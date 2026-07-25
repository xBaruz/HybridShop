namespace HybridShop.Services.Order.Application.Dto;

public record OrderItemDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public Guid? SkuId { get; init; } 
    public string Title { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public Guid SellerId { get; init; }
    public string Status { get; init; } = string.Empty;
}