using HybridShop.Services.Order.Application.Dto;
using HybridShop.Services.Order.Application.Exceptions;
using HybridShop.Services.Order.Core.Interfaces;
using HybridShop.Services.Order.Core.Models.ShoppingCart;
using HybridShop.Services.Order.Core.Models.Dto;
using HybridShop.Services.Order.Core.Models.Delivery;
using HybridShop.Services.Order.Core.Exceptions;

namespace HybridShop.Services.Order.Application.Services;

public class ShoppingCartService
{
    private readonly IShoppingCartRepository _repository;
    private readonly IProductServiceClient _productClient; 

    public ShoppingCartService(
        IShoppingCartRepository repository,
        IProductServiceClient productClient
    )
    {
        _repository = repository;
        _productClient = productClient;
    }

    public async Task<ShoppingCartDto> GetCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await _repository.GetCartAsync(userId, cancellationToken);
        
        if (cart is null)
        {
            var newCart = ShoppingCart.NewShoppingCart(userId);
            await _repository.UpdateCartAsync(newCart, cancellationToken);
            return new ShoppingCartDto { UserId = userId, CartVersion = newCart.Version };
        }

        if (!cart.Items.Any())
        {
            return new ShoppingCartDto 
            { 
                UserId = cart.UserId, 
                CartVersion = cart.Version,
                DeliveryCost = cart.Delivery?.Price ?? 0m,
                DeliveryName = cart.Delivery?.Name,
                Total = cart.Delivery?.Price ?? 0m,
                Items = new List<CartItemDto>()
            };
        }

        var requestItems = cart.Items.Select(i => (i.ProductId, i.SkuId)).ToList();
        var fetchedProducts = await _productClient.GetProductsByIdsAsync(requestItems, cancellationToken);

        var productsDict = fetchedProducts
            .Where(p => p.ProductId != Guid.Empty)
            .GroupBy(p => p.ProductId)
            .ToDictionary(g => g.Key, g => g.First());

        var itemsDto = new List<CartItemDto>();
        decimal itemsSubtotal = 0m;

        foreach (var i in cart.Items)
        {
            productsDict.TryGetValue(i.ProductId, out var product);

            var title = product is not null && !string.IsNullOrWhiteSpace(product.Title) 
                ? product.Title 
                : "Produkt niedostępny";

            var price = product is not null && product.Price > 0 
                ? product.Price 
                : i.Price;

            var sellerId = product is not null && product.SellerId != Guid.Empty 
                ? product.SellerId 
                : i.SellerId;

            itemsSubtotal += price * i.Quantity.Value;

            itemsDto.Add(new CartItemDto
            {
                ProductId = i.ProductId,
                SkuId = i.SkuId,
                Quantity = i.Quantity.Value,
                Title = title,
                Price = price,
                SellerId = sellerId
            });
        }

        var deliveryCost = cart.Delivery?.Price ?? 0m;
        decimal total = itemsSubtotal + deliveryCost;

        return new ShoppingCartDto
        {
            UserId = cart.UserId,
            CartVersion = cart.Version,
            Total = total,
            DeliveryCost = deliveryCost,
            DeliveryName = cart.Delivery?.Name,
            Items = itemsDto
        };
    }

    public async Task AddItemToCartAsync(Guid userId, AddCartItemDto dto, CancellationToken cancellationToken = default)
    {
        var fetchedProducts = await _productClient.GetProductsByIdsAsync(new[] { (dto.ProductId, dto.SkuId) }, cancellationToken);
        
        var product = fetchedProducts.FirstOrDefault(p => p.ProductId == dto.ProductId);

        if (product is null)
            throw new ProductNotFoundException(dto.ProductId);

        var cart = await _repository.GetCartAsync(userId, cancellationToken) ?? ShoppingCart.NewShoppingCart(userId);

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == dto.ProductId && i.SkuId == product.SkuId);
        int currentQuantityInCart = existingItem?.Quantity.Value ?? 0;
        int totalRequestedQuantity = currentQuantityInCart + dto.Quantity;

        if (totalRequestedQuantity > product.Quantity)
        {
            throw new InsufficientStockException(totalRequestedQuantity, product.Quantity);
        }
        
        cart.AddItem(
            product.ProductId,
            product.SkuId, 
            new Quantity(dto.Quantity),
            product.Price,
            product.SellerId
        );

        await _repository.UpdateCartAsync(cart, cancellationToken);
    }

    public async Task RemoveItemFromCartAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ShoppingCartService] Usuwanie produktu ProductId: {productId} z koszyka UserId: {userId}");
        var cart = await _repository.GetCartAsync(userId, cancellationToken);
        if (cart is null)
            throw new CartNotFoundException();

        cart.RemoveItem(productId);
        await _repository.UpdateCartAsync(cart, cancellationToken);
    }

    public async Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ShoppingCartService] Czyszczenie koszyka UserId: {userId}");
        var cart = await _repository.GetCartAsync(userId, cancellationToken);
        if (cart is null)
            throw new CartNotFoundException();

        await _repository.DeleteCartAsync(userId, cancellationToken);
    }

    public async Task SetDeliveryMethodAsync(Guid userId, SetDeliveryMethodDto dto, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ShoppingCartService] Ustawianie metody dostawy dla UserId: {userId}, DeliveryTypeId: {dto.DeliveryTypeId}");
        if (!Enum.IsDefined(typeof(DeliveryType), dto.DeliveryTypeId))
            throw new InvalidDeliveryOptionException();

        var cart = await _repository.GetCartAsync(userId, cancellationToken);
        if (cart is null)
            throw new CartNotFoundException();

        var option = DeliveryOption.Create((DeliveryType)dto.DeliveryTypeId);
        var delivery = DeliveryMethod.ChooseDelivery(option.Name, option.Price);
        cart.SetDeliveryMethod(delivery);

        await _repository.UpdateCartAsync(cart, cancellationToken);
    }

    public Task<List<DeliveryOptionDto>> GetAvailableDeliveryMethodsAsync()
    {
        var options = Enum.GetValues<DeliveryType>()
            .Select(type => 
            {
                var option = DeliveryOption.Create(type);
                return new DeliveryOptionDto
                {
                    Id = (int)option.Type,
                    Name = option.Name,
                    Price = option.Price
                };
            })
            .ToList();

        return Task.FromResult(options);
    }
}