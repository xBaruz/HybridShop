namespace HybridShop.Services.Order.Core.Models.Order;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid? SkuId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal Price { get; private set; }
    public Guid SellerId { get; private set; }
    public OrderStatus Status { get; private set; }

    private OrderItem() { }

    public static OrderItem AddOrderItem(
        Guid productId,
        string title,
        int quantity,
        decimal price,
        Guid sellerId,
        Guid? skuId = null)
    {
        return new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            SkuId = skuId,
            Title = title,
            Quantity = quantity,
            Price = price,
            SellerId = sellerId,
            Status = OrderStatus.Placed
        };
    }

    public void UpdateStatus(OrderStatus status)
    {
        Status = status;
    }
}