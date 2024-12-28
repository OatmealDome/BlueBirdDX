using BlueBirdDX.WebApp.Models;
using NATS.Net;

namespace BlueBirdDX.WebApp.Services;

public class NotificationService
{
    public readonly NatsClient Client;

    public NotificationService(NotificationSettings settings)
    {
        Client = new NatsClient(settings.Server);
    }
}