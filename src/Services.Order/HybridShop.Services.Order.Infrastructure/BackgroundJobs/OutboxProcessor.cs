using System.Text.Json;
using HybridShop.Services.Order.Core.Interfaces;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HybridShop.Services.Order.Infrastructure.BackgroundJobs;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

                var messages = await outboxRepository.GetUnprocessedMessagesAsync(20, stoppingToken);

                if (messages.Any())
                {
                    foreach (var message in messages)
                    {
                        try
                        {
                            var type = Type.GetType(message.Type);

                            if (type is null)
                            {
                                var typeNameOnly = message.Type.Split(',')[0].Trim();
                                type = AppDomain.CurrentDomain.GetAssemblies()
                                    .SelectMany(a => a.GetTypes())
                                    .FirstOrDefault(t => t.FullName == typeNameOnly);
                            }

                            if (type is null)
                            {
                                _logger.LogWarning("Nie znaleziono typu zdarzenia Outbox: {Type}", message.Type);
                                message.MarkAsFailed("Nie znaleziono typu zdarzenia.");
                                continue;
                            }

                            var eventMessage = JsonSerializer.Deserialize(message.Content, type);
                            if (eventMessage is not null)
                            {
                                await publishEndpoint.PublishGeneric(eventMessage, type, stoppingToken);

                                message.MarkAsProcessed();
                                _logger.LogInformation("Outbox pomyślnie wysłał zdarzenie {Type} [ID: {Id}]", message.Type, message.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Błąd przetwarzania wiadomości Outbox {Id}", message.Id);
                            message.MarkAsFailed(ex.Message);
                        }
                    }

                    await unitOfWork.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pętli OutboxProcessor.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}