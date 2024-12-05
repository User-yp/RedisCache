using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace RedisCache.MediatREvent;

public static class MediatRExtension
{
    public static IServiceCollection AddMediatR(this IServiceCollection service)
    {
        service.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        service.AddSingleton<IDomainEvents, DomainEvents>();
        return service;
    }
}
