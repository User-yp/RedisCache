using MediatR;

namespace RedisCache.MediatREvent;

public class DomainEvents : IDomainEvents
{
    private readonly IMediator mediator;
    private readonly Dictionary<Type, List<INotification>> EventCenter = new Dictionary<Type, List<INotification>>();

    public DomainEvents(IMediator mediator)
    {
        this.mediator = mediator;
    }

    public void AddEvent(INotification item)
    {
        EventCenter.AddEvnet(item);
    }
    public void AddEventIfNoExist(INotification item)
    {
        EventCenter.AddEvnetIfNoExist(item);
    }
    public async Task DirPublishAsync(INotification item)
    {
        await mediator.Publish(item);
    }
    public async Task PublishAsync(INotification item)
    {
        if (EventCenter.TryGetValue(item.GetType(), out var events))
        {
            if (events.Contains(item))
            {
                await mediator.Publish(item);
                EventCenter.RemoveEvent(item);
            }
        }
    }
    public async Task PublishAsync<T>()
    {
        if (EventCenter.TryGetValue(typeof(T), out var events))
        {
            foreach (var _event in events)
            {
                await mediator.Publish(_event);
            }
        }
        EventCenter.Remove(typeof(T));
    }
    public async Task PublishAllAsync()
    {
        var events = GetAllEvents();
        foreach (var _event in events)
        {
            await mediator.Publish(_event);
        }
        ClearAllEvents();
    }
    private List<INotification> GetAllEvents()
    {
        List<INotification> events = new List<INotification>();
        foreach (var notification in EventCenter.Values)
        {
            events.AddRange(notification);
        }
        return events;
    }
    private void ClearAllEvents()
    {
        EventCenter.Clear();
    }
}
