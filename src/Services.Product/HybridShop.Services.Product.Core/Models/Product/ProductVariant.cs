namespace HybridShop.Services.Product.Core.Product;

public class ProductVariant
{
    public ProductVariant()
    {
        Price = null!;
        Quantity = null!;
        Attributes = new Dictionary<string, object>();
        Images = new List<string>();
    }

    public ProductVariant(
        Guid skuId,
        Price price,
        Quantity quantity,
        Dictionary<string, object> attributes,
        List<string>? images = null
    )
    {
        SkuId = skuId;
        Price = price;
        Quantity = quantity;
        Attributes = attributes;
        Images = images ?? new List<string>();
    }

    public Guid SkuId { get; private set; }
    public Price Price { get; private set; }
    public Quantity Quantity { get; private set; }
    public Dictionary<string, object> Attributes { get; private set; }
    public List<string> Images { get; private set; }

    public void AddImage(string imageUrl)
    {
        Images.Add(imageUrl);
    }

    public void RemoveImage(string imageUrl)
    {
        Images.Remove(imageUrl);
    }

    public void UpdateQuantity(int value) => Quantity = new Quantity(value);
}