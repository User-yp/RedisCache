using MediatR;

namespace RedisCache.MediatREvent;

public static class EventExtension
{
    public static void AddEvnet(this Dictionary<Type, List<INotification>> eventCenter, INotification item)
    {
        Type type = item.GetType();
        if (!eventCenter.TryGetValue(type, out var value))
        {
            value = new List<INotification>();
            eventCenter[type] = value;
        }
        value.Add(item);
    }
    public static void AddEvnetIfNoExist(this Dictionary<Type, List<INotification>> eventCenter, INotification item)
    {
        Type type = item.GetType();
        if (!eventCenter.TryGetValue(type, out var value))
        {
            value = new List<INotification>();
            eventCenter[type] = value;
        }
        if (!value.Contains(item))
            value.Add(item);
    }
    public static void RemoveEvent(this Dictionary<Type, List<INotification>> eventCenter, INotification item)
    {
        Type type = item.GetType();
        if (eventCenter.TryGetValue(type, out var value))
            value.Remove(item);
    }
}
