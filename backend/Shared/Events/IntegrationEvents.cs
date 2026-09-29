namespace NextStep.Shared.Events;

/// <summary>
/// A fact published by one module that other modules may react to
/// (e.g. "candidatures deleted" → Messaging and Coaching clean up their own rows).
/// Replaces cross-module foreign keys and cascades.
/// </summary>
public interface IIntegrationEvent;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}

public interface IEventPublisher
{
    /// <summary>
    /// Delivers the event to every registered handler, after the publisher's own
    /// transaction is committed. A failing handler is logged and does not fail the publisher.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIntegrationEvent;
}

/// <summary>
/// In-process dispatcher (modular monolith). Once a module becomes a service,
/// the same events go through a message broker with no change in the handlers.
/// </summary>
public class InProcessEventPublisher(IServiceProvider services, ILogger<InProcessEventPublisher> logger) : IEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : IIntegrationEvent
    {
        foreach (var handler in services.GetServices<IIntegrationEventHandler<TEvent>>())
        {
            try
            {
                await handler.HandleAsync(integrationEvent, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Integration event {Event} failed in handler {Handler}",
                    typeof(TEvent).Name, handler.GetType().Name);
            }
        }
    }
}
