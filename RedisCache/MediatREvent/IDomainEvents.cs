using MediatR;

namespace RedisCache.MediatREvent;

public interface IDomainEvents
{
    void AddEvent(INotification item);
    void AddEventIfNoExist(INotification item);
    Task DirPublishAsync(INotification item);
    Task PublishAsync(INotification item);
    Task PublishAsync<T>();
    Task PublishAllAsync();
}