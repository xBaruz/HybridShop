using HybridShop.Services.Auth.Infrastructure.Data;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace HybridShop.Services.Auth.Tests;

public class AuthWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("HybridShopTestDb")
        .WithUsername("shopuser")
        .WithPassword("shoppassword")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        LoadEnv();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var connString = $"{_postgresContainer.GetConnectionString()};Pooling=false";
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connString
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));

            if (dbDescriptor != null)
            {
                services.Remove(dbDescriptor);
            }

            var connString = $"{_postgresContainer.GetConnectionString()};Pooling=false";
            services.AddDbContext<AuthDbContext>(options =>
            {
                options.UseNpgsql(connString);
            });

            services.AddMassTransitTestHarness();
        });
    }

    private static void LoadEnv()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var envPath = Path.Combine(dir.FullName, ".env");
            if (File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
                break;
            }
            dir = dir.Parent;
        }
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }
}