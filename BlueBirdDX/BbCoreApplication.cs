using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Database;
using BlueBirdDX.Database.Migration.AccountGroup;
using BlueBirdDX.Database.Migration.MediaUploadJob;
using BlueBirdDX.Database.Migration.PostThread;
using BlueBirdDX.Database.Migration.UploadedMedia;
using BlueBirdDX.Grpc;
using BlueBirdDX.Media;
using BlueBirdDX.Social;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using OatmealDome.Slab;
using OatmealDome.Slab.Mongo;
using OatmealDome.Slab.S3;
using OatmealDome.Slab.Web;
using Quartz;

namespace BlueBirdDX;

public class BbCoreApplication : SlabWebApplication
{
    protected override string ApplicationName => "BlueBirdDX.Core";

    // TODO: Can we better integrate this with Slab?
    protected override WebApplicationBuilder CreateBuilder(string[]? args)
    {
        WebApplicationBuilder builder = base.CreateBuilder(args);
        
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureEndpointDefaults(listenOptions =>
            {
                listenOptions.Protocols = HttpProtocols.Http2;
            });
        });

        return builder;
    }

    protected override void BuildApplication(ISlabApplicationBuilder appBuilder)
    {
        appBuilder.Services.AddGrpc();

        appBuilder.RegisterMongo(b => b
            .AddCollection<AccountGroup>("accounts")
            .AddMigrator<AccountGroup, AccountGroupMigratorOneToTwo>()
            .AddMigrator<AccountGroup, AccountGroupMigratorTwoToThree>()
            .AddMigrator<AccountGroup, AccountGroupMigratorThreeToFour>()
            .AddMigrator<AccountGroup, AccountGroupMigratorFourToFive>()
            .AddCollection<MediaUploadJob>("media_jobs")
            .AddMigrator<MediaUploadJob, MediaUploadJobMigratorOneToTwo>()
            .AddCollection<UploadedMedia>("media")
            .AddMigrator<UploadedMedia, UploadedMediaMigratorOneToTwo>()
            .AddMigrator<UploadedMedia, UploadedMediaMigratorTwoToThree>()
            .AddCollection<PostThread>("threads")
            .AddMigrator<PostThread, PostThreadMigratorOneToTwo>()
            .AddMigrator<PostThread, PostThreadMigratorTwoToThree>()
            .AddMigrator<PostThread, PostThreadMigratorThreeToFour>());
        
        appBuilder.RegisterS3();
        
        appBuilder.RegisterSingleton<AccountGroupManager>();
        
        appBuilder.RegisterConfiguration<MediaUploadJobManagerConfiguration>("MediaUploadJobManager");
        appBuilder.RegisterHostedService<MediaUploadJobManager>();
        
        appBuilder.RegisterConfiguration<SocialAppConfiguration>("SocialApp");
        
        appBuilder.RegisterConfiguration<PostThreadManagerConfiguration>("PostThreadManager");
        appBuilder.RegisterSingleton<PostThreadManager>();
        
        appBuilder.RegisterJob<PostThreadManagerProcessJob>(SlabJobKey.Create("PostThreadManagerProcessJob"), t => t
            .StartAt(DateTime.Now.GetNextInterval(TimeSpan.FromMinutes(1)))
            .WithSimpleSchedule(builder => builder
                .WithIntervalInMinutes(1)
                .RepeatForever()));

        appBuilder.RegisterJob<MediaUploadJobManagerCleanUpJob>(SlabJobKey.Create("MediaUploadJobManagerCleanUpJob"),
            t => t
                .StartAt(DateTime.UtcNow)
                .WithSimpleSchedule(builder => builder
                    .WithIntervalInHours(24)
                    .RepeatForever()));

        appBuilder.RegisterJob<AccountGroupManagerRefreshThreadsJob>(
            SlabJobKey.Create("AccountGroupManagerRefreshThreadsJob"), t => t
                .StartAt(DateTime.UtcNow)
                .WithSimpleSchedule(builder => builder
                    .WithIntervalInHours(24)
                    .RepeatForever()));
    }

    protected override void SetupApplication(WebApplication app)
    {
        app.MapGrpcService<SocialAppAuthorizationGrpcService>();
        app.MapGrpcService<MediaUploadJobManagerRemoteServiceWrapper>();
        app.MapGrpcService<PostThreadManagerRemoteServiceWrapper>();
        app.MapGet("/", () => "OK");
    }
}
