using BlueBirdDX.WebApp.Models;
using Microsoft.Extensions.Options;
using NATS.Net;

namespace BlueBirdDX.WebApp.Services;

public class NotificationService
{
    public readonly NatsClient Client;

    public NotificationService(IOptions<NotificationSettings> settings)
    {
        Client = new NatsClient(settings.Value.Server);
    }
}
