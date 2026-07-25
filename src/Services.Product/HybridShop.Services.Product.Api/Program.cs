using HybridShop.BuildingBlocks.EventBus;
using HybridShop.BuildingBlocks.OpenApi;
using HybridShop.BuildingBlocks.OpenApi.Auth;
using HybridShop.Services.Product.Api.GraphQL;
using HybridShop.Services.Product.Application;
using HybridShop.Services.Product.Application.Consumers;
using HybridShop.Services.Product.Infrastructure;
using HybridShop.Services.Product.Infrastructure.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenAnyIP(8081, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddGrpc();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddType<AnyType>();

builder.Services.AddSharedOpenApi();
builder.Services.AddAuthServices(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddEventBus(builder.Configuration, x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600;
});

var app = builder.Build();

app.MapOpenApi("/api/product/openapi/{documentName}.json");

app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL("/graphql");

app.MapGrpcService<ProductGrpcServerService>();

app.MapControllers();

app.Run();