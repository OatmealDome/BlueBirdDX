using BlueBirdDX.Common.Storage;
using BlueBirdDX.WebApp.Models;

namespace BlueBirdDX.WebApp.Services;

public class RemoteStorageService
{
    public readonly RemoteStorage SharedInstance;
    
    public RemoteStorageService(RemoteStorageSettings settings)
    {
        SharedInstance = new RemoteStorage(settings.ServiceUrl, settings.Bucket, settings.AccessKey,
            settings.AccessKeySecret);
    }
}