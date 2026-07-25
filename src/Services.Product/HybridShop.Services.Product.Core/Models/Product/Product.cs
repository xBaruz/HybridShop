namespace HybridShop.Services.Product.Core.Product;

public class Product
{
    public Product()
    {
        Price = null!;
        Quantity = null!;
        Status = null!;
        Category = null!;
        Attributes = new Dictionary<string, object>();
        Variants = new List<ProductVariant>();
        Images = new List<string>();
    }

    private Product(
        Guid id,
        string title,
        string slug,
        string? description,
        Price price,
        Quantity quantity,
        ProductStatus status,
        Guid sellerId,
        DateTime createdAt,
        bool isDeleted,
        ProductCategory category,
        Dictionary<string, object>? attributes,
        List<ProductVariant>? variants,
        List<string>? images
    )
    {
        Id = id;
        Title = title;
        Slug = slug;
        Description = description;
        Price = price;
        Quantity = quantity;
        Status = status;
        SellerId = sellerId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        IsDeleted = isDeleted;
        Category = category;
        Attributes = attributes ?? new Dictionary<string, object>();
        Variants = variants ?? new List<ProductVariant>();
        Images = images ?? new List<string>();
    }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; } = string.Empty;
    public Price Price { get; private set; }
    public Quantity Quantity { get; private set; }
    public ProductStatus Status { get; private set; }
    public Guid SellerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public ProductCategory Category { get; private set; }
    public Dictionary<string, object> Attributes { get; private set; }
    public List<ProductVariant> Variants { get; private set; }
    public List<string> Images { get; private set; }

    public static Product NewProduct(
        string title,
        string slug,
        string? description,
        Price price,
        Quantity quantity,
        Guid sellerId,
        ProductCategory category,
        Dictionary<string, object>? attributes = null,
        List<ProductVariant>? variants = null,
        List<string>? images = null
    )
    {
        return new Product(
            Guid.NewGuid(), 
            title, 
            slug, 
            description, 
            price,
            quantity,
            ProductStatus.Active(), 
            sellerId, 
            DateTime.UtcNow, 
            false,
            category,
            attributes,
            variants,
            images
        );
    }

    public void AddImage(string imageUrl)
    {
        Images.Add(imageUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveImage(string imageUrl)
    {
        Images.Remove(imageUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddVariantImage(Guid skuId, string imageUrl)
    {
        var variant = Variants.FirstOrDefault(v => v.SkuId == skuId);
        if (variant is null)
        {
            throw new KeyNotFoundException($"Nie znaleziono wariantu o SkuId: {skuId}");
        }

        variant.AddImage(imageUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveVariantImage(Guid skuId, string imageUrl)
    {
        var variant = Variants.FirstOrDefault(v => v.SkuId == skuId);
        if (variant is null)
        {
            throw new KeyNotFoundException($"Nie znaleziono wariantu o SkuId: {skuId}");
        }

        variant.RemoveImage(imageUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(
        string title,
        string slug,
        string? description,
        Price price,
        Quantity quantity,
        ProductCategory category,
        Dictionary<string, object>? attributes,
        List<ProductVariant>? variants
    )
    {
        Title = title;
        Slug = slug;
        Description = description;
        Price = price;
        Quantity = quantity;
        Category = category;
        Attributes = attributes ?? new Dictionary<string, object>();
        Variants = variants ?? new List<ProductVariant>();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;   
    }

    public void DecreaseQuantity(int amount, Guid? skuId = null)
    {
        if (skuId.HasValue && skuId != Guid.Empty)
        {
            var variant = Variants.FirstOrDefault(v => v.SkuId == skuId.Value);
            if (variant is not null)
            {
                var newVarVal = Math.Max(0, variant.Quantity.Value - amount);
                variant.UpdateQuantity(newVarVal); 
            }
        }
        
        var newVal = Math.Max(0, Quantity.Value - amount);
        Quantity = new Quantity(newVal);

        UpdatedAt = DateTime.UtcNow;
    }
}

