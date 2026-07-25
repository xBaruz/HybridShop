using HybridShop.BuildingBlocks.EventBus.Events;
using HybridShop.Services.Product.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace HybridShop.Services.Product.Application.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(IProductRepository productRepository, ILogger<OrderCreatedConsumer> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var orderEvent = context.Message;
        _logger.LogInformation("Otrzymano OrderCreatedEvent dla zamówienia: {OrderId}", orderEvent.OrderId);

        foreach (var item in orderEvent.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, context.CancellationToken);

            if (product is null)
            {
                _logger.LogWarning("Nie znaleziono produktu o ID: {ProductId} podczas aktualizacji stanu magazynowego.", item.ProductId);
                continue;
            }

            product.DecreaseQuantity(item.Quantity, item.SkuId);

            await _productRepository.UpdateAsync(product, context.CancellationToken);
            
            _logger.LogInformation("Zaktualizowano stan magazynowy produktu {ProductId} (Sku: {SkuId}). Odjęto: {Quantity}", 
                item.ProductId, item.SkuId, item.Quantity);
        }
    }
}