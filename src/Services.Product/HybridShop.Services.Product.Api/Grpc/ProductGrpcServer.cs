using Grpc.Core;
using HybridShop.Services.Product.Core.Interfaces;
using HybridShop.Services.Product.Grpc;

namespace HybridShop.Services.Product.Infrastructure.Grpc;

public class ProductGrpcServerService : ProductGrpcService.ProductGrpcServiceBase
{
    private readonly IProductRepository _productRepository;

    public ProductGrpcServerService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public override async Task<GetProductsByIdsResponse> GetProductsByIds(
        GetProductsByIdsRequest request, 
        ServerCallContext context)
    {
        if (request.Items == null || !request.Items.Any())
        {
            return new GetProductsByIdsResponse();
        }

        var parsedItems = new List<(Guid ProductId, Guid? SkuId)>();

        foreach (var item in request.Items)
        {
            if (!Guid.TryParse(item.ProductId, out var prodId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid Product ID format: {item.ProductId}"));
            }

            Guid? skuId = null;
            if (!string.IsNullOrWhiteSpace(item.SkuId))
            {
                if (!Guid.TryParse(item.SkuId, out var parsedSku))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid SKU ID format: {item.SkuId}"));
                }
                skuId = parsedSku;
            }

            parsedItems.Add((prodId, skuId));
        }

        var productIdsToFetch = parsedItems.Select(x => x.ProductId).Distinct().ToList();
        var products = await _productRepository.GetByIdsAsync(productIdsToFetch, context.CancellationToken);

        var productDict = products
            .GroupBy(p => p.Id.ToString().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var response = new GetProductsByIdsResponse();

        foreach (var item in parsedItems)
        {
            var lookupKey = item.ProductId.ToString().ToLowerInvariant();

            if (!productDict.TryGetValue(lookupKey, out var product))
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Product with ID '{item.ProductId}' was not found."));
            }

            bool hasVariants = product.Variants is not null && product.Variants.Any();

            Guid? finalSkuId = null;
            double finalPrice = (double)product.Price.Value;
            int finalQuantity = product.Quantity.Value;

            if (hasVariants && item.SkuId.HasValue)
            {
                var matchingVariant = product.Variants!.FirstOrDefault(v => v.SkuId == item.SkuId.Value);

                if (matchingVariant is null)
                {
                    throw new RpcException(new Status(
                        StatusCode.InvalidArgument, 
                        $"SKU '{item.SkuId.Value}' does not belong to Product '{item.ProductId}'."));
                }

                finalSkuId = item.SkuId.Value;
                finalPrice = (double)matchingVariant.Price.Value;
                finalQuantity = matchingVariant.Quantity.Value;
            }

            var model = new ProductGrpcModel
            {
                ProductId = product.Id.ToString(),
                SkuId = finalSkuId.HasValue ? finalSkuId.Value.ToString() : string.Empty,
                Title = product.Title,
                Price = finalPrice,
                Quantity = finalQuantity,
                SellerId = product.SellerId.ToString()
            };

            response.Products.Add(model);
        }

        return response;
    }
}