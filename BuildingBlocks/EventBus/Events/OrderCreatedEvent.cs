namespace HybridShop.BuildingBlocks.EventBus.Events;

public record OrderItemDto(
    Guid ProductId,
    Guid? SkuId,
    int Quantity,
    decimal Price
);

public record OrderCreatedEvent(
    Guid OrderId,
    Guid BuyerId,
    Guid SellerId,
    string BuyerEmail,
    decimal TotalAmount,
    List<OrderItemDto> Items
)
{
    public decimal Total => TotalAmount;
    public List<OrderItemDto> OrderItems => Items;
}