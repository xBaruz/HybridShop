using System.Text.Json;
using HybridShop.BuildingBlocks.EventBus.Events;
using HybridShop.Services.Order.Application.Dto;
using HybridShop.Services.Order.Application.Exceptions;
using HybridShop.Services.Order.Core.Interfaces;
using HybridShop.Services.Order.Core.Models.Order;
using HybridShop.Services.Order.Core.Models.Outbox;

using EventOrderItemDto = HybridShop.BuildingBlocks.EventBus.Events;

namespace HybridShop.Services.Order.Application.Services;

public class OrderService
{
    private readonly IShoppingCartRepository _cartRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IProductServiceClient _productClient;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        IShoppingCartRepository cartRepository,
        IOrderRepository orderRepository,
        IOutboxRepository outboxRepository,
        IProductServiceClient productClient,
        IUnitOfWork unitOfWork
    )
    {
        _cartRepository = cartRepository;
        _orderRepository = orderRepository;
        _outboxRepository = outboxRepository;
        _productClient = productClient;
        _unitOfWork = unitOfWork;
    }

    public async Task<List<OrderDto>> CreateOrdersFromCartAsync(Guid userId, string buyerEmail, CreateOrderDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(dto.ShippingAddress))
            throw new InvalidDeliveryAddressException();

        var cart = await _cartRepository.GetCartAsync(userId, cancellationToken);
        if (cart is null || !cart.Items.Any())
            throw new CartIsEmptyOrDoesntExistException();

        if (cart.Delivery is null)
            throw new InvalidOperationException("Delivery method must be set before creating an order.");

        if (cart.Version != dto.CartVersion)
            throw new CartConcurrencyException();

        var requestItems = cart.Items.Select(i => (i.ProductId, i.SkuId)).ToList();
        var fetchedProducts = await _productClient.GetProductsByIdsAsync(requestItems, cancellationToken);

        var productsDict = fetchedProducts
            .Where(p => p.ProductId != Guid.Empty)
            .GroupBy(p => (p.ProductId, p.SkuId))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var item in cart.Items)
        {
            if (!productsDict.TryGetValue((item.ProductId, item.SkuId), out var product))
            {
                if (!productsDict.TryGetValue((item.ProductId, null), out product))
                {
                    throw new ProductNotFoundException(item.ProductId);
                }
            }

            if (item.Quantity.Value > product.Quantity)
            {
                throw new InsufficientStockException(product.Quantity, item.Quantity.Value);
            }
        }

        var finalCheckCart = await _cartRepository.GetCartAsync(userId, cancellationToken);
        if (finalCheckCart is null || finalCheckCart.Version != dto.CartVersion)
            throw new CartConcurrencyException();

        await _cartRepository.DeleteCartAsync(userId, cancellationToken);

        var groupedItems = cart.Items.GroupBy(i =>
        {
            if (!productsDict.TryGetValue((i.ProductId, i.SkuId), out var product))
            {
                product = productsDict[(i.ProductId, null)];
            }
            return product.SellerId != Guid.Empty ? product.SellerId : i.SellerId;
        });

        var createdOrders = new List<Core.Models.Order.Order>();

        foreach (var group in groupedItems)
        {
            var sellerItems = group.Select(i =>
            {
                if (!productsDict.TryGetValue((i.ProductId, i.SkuId), out var product))
                {
                    product = productsDict[(i.ProductId, null)];
                }

                return OrderItem.AddOrderItem(
                    i.ProductId,
                    product.Title,
                    i.Quantity.Value,
                    product.Price,
                    group.Key,
                    i.SkuId
                );
            }).ToList();

            var order = Core.Models.Order.Order.Create(
                userId,
                sellerItems,
                cart.Delivery.Price,
                cart.Delivery.Name,
                dto.ShippingAddress
            );

            createdOrders.Add(order);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var order in createdOrders)
            {
                await _orderRepository.AddAsync(order, cancellationToken);

                var sellerId = order.Items.First().SellerId;

                var itemsDto = order.Items.Select(item => new EventOrderItemDto.OrderItemDto(
                    item.ProductId,
                    item.SkuId,
                    item.Quantity,
                    item.Price
                )).ToList();

                var @event = new OrderCreatedEvent(
                    order.Id,
                    order.BuyerId,
                    sellerId,
                    buyerEmail,
                    order.Total,
                    itemsDto
                );

                var outboxMessage = new OutboxMessage(
                    typeof(OrderCreatedEvent).AssemblyQualifiedName!,
                    JsonSerializer.Serialize(@event)
                );

                await _outboxRepository.AddAsync(outboxMessage, cancellationToken);
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return createdOrders.Select(MapToDto).ToList();
    }

    public async Task<IEnumerable<OrderDto>> GetBuyerOrdersAsync(Guid buyerId, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (buyerId != currentUserId)
            throw new UnauthorizedException();

        var orders = await _orderRepository.GetByBuyerIdAsync(buyerId, cancellationToken);
        return orders.Select(MapToDto);
    }

    public async Task<IEnumerable<OrderDto>> GetSellerOrdersAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetBySellerIdAsync(sellerId, cancellationToken);
        return orders.Select(MapToDto);
    }

    public async Task UpdateOrderItemStatusAsync(Guid orderId, Guid orderItemId, OrderStatus status, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null)
            throw new OrderNotFoundException();

        var item = order.Items.FirstOrDefault(i => i.Id == orderItemId);
        if (item is null)
            throw new OrderItemNotFoundException();

        var isSeller = item.SellerId == currentUserId;
        var isBuyerCancelling = order.BuyerId == currentUserId 
                               && item.Status == OrderStatus.Placed 
                               && status == OrderStatus.Cancelled;

        if (!isSeller && !isBuyerCancelling)
            throw new UnauthorizedException();

        item.UpdateStatus(status);

        if (order.Items.All(i => i.Status == OrderStatus.Cancelled))
        {
            order.UpdateStatus(OrderStatus.Cancelled);
        }
        else if (order.Items.All(i => i.Status == OrderStatus.Completed))
        {
            order.UpdateStatus(OrderStatus.Completed);
        }
        else if (order.Items.Any(i => i.Status == OrderStatus.Shipped))
        {
            order.UpdateStatus(OrderStatus.Shipped);
        }

        await _orderRepository.UpdateAsync(order, cancellationToken);
    }

    private static OrderDto MapToDto(Core.Models.Order.Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            BuyerId = order.BuyerId,
            DeliveryPrice = order.DeliveryPrice,
            DeliveryName = order.DeliveryName,
            Total = order.Total,
            ShippingAddress = order.ShippingAddress,
            Status = order.Status.ToString(),
            CreatedAt = order.CreatedAt,
            Items = order.Items?.Select(i => new HybridShop.Services.Order.Application.Dto.OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                SkuId = i.SkuId,
                Title = i.Title,
                Quantity = i.Quantity,
                Price = i.Price,
                SellerId = i.SellerId,
                Status = i.Status.ToString()
            }).ToList() ?? new List<HybridShop.Services.Order.Application.Dto.OrderItemDto>()
        };
    }
}