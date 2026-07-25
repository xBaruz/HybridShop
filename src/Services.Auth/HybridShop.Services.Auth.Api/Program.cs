using HybridShop.BuildingBlocks.EventBus;
using HybridShop.BuildingBlocks.OpenApi;
using HybridShop.BuildingBlocks.OpenApi.Auth;
using HybridShop.Services.Auth.Api.Grpc;
using HybridShop.Services.Auth.Application;
using HybridShop.Services.Auth.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenAnyIP(8081, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddSharedOpenApi();
builder.Services.AddAuthServices(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddEventBus(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();

app.MapOpenApi("/api/auth/openapi/{documentName}.json");

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<UserGrpcServer>();
app.MapControllers();

app.Run();

