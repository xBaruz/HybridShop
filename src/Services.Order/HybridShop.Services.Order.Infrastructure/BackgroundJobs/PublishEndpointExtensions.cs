using MassTransit;
using System.Collections.Concurrent;
using System.Reflection;

namespace HybridShop.Services.Order.Infrastructure.BackgroundJobs;

public static class PublishEndpointExtensions
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> _publishMethods = new();

    public static Task PublishGeneric(this IPublishEndpoint publishEndpoint, object message, Type messageType, CancellationToken cancellationToken = default)
    {
        var method = _publishMethods.GetOrAdd(messageType, type =>
        {
            return typeof(IPublishEndpoint)
                .GetMethods()
                .First(m => m.Name == nameof(IPublishEndpoint.Publish) 
                            && m.IsGenericMethod 
                            && m.GetParameters().Length == 2 
                            && m.GetParameters()[1].ParameterType == typeof(CancellationToken))
                .MakeGenericMethod(type);
        });

        return (Task)method.Invoke(publishEndpoint, new[] { message, cancellationToken })!;
    }
}