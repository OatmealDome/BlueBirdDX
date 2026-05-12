using BlueBirdDX.Common.Account;
using BlueBirdDX.Common.Media;
using BlueBirdDX.Common.Post;
using BlueBirdDX.Grpc;
using BlueBirdDX.WebApp.Models;
using BlueBirdDX.WebApp.Services;
using OatmealDome.Slab;
using OatmealDome.Slab.Mongo;
using OatmealDome.Slab.S3;
using OatmealDome.Slab.Web;

namespace BlueBirdDX.WebApp;

public class BbWebApplication : SlabWebApplication
{
    protected override string ApplicationName => "BlueBirdDX.WebApp";

    protected override void BuildApplication(ISlabApplicationBuilder appBuilder)
    {
        appBuilder.Services.AddRazorPages();
        appBuilder.Services.AddGrpcClient<SocialAppAuthorization.SocialAppAuthorizationClient>(options =>
        {
            string coreServiceUrl = appBuilder.Configuration.GetValue<string>("Grpc:CoreUrl") ?? "http://core";
            options.Address = new Uri(coreServiceUrl);
        });

        appBuilder.RegisterMongo(b => b
            .AddCollection<AccountGroup>("accounts")
            .AddCollection<UploadedMedia>("media")
            .AddCollection<MediaUploadJob>("media_jobs")
            .AddCollection<PostThread>("threads"));
        
        appBuilder.RegisterS3();
        
        appBuilder.RegisterConfiguration<TextWrapperSettings>("TextWrapper");
        appBuilder.RegisterSingleton<TextWrapperService>();
        
        appBuilder.RegisterConfiguration<NotificationSettings>("Notifications");
        appBuilder.RegisterSingleton<NotificationService>();
        
        appBuilder.RegisterConfiguration<SocialAppAuthorizationSettings>("SocialAppAuthorization");
        appBuilder.RegisterSingleton<TwitterAuthorizationService>();
        appBuilder.RegisterSingleton<ThreadsAuthorizationService>();
        
        appBuilder.RegisterConfiguration<DatabaseSettings>("Database");
    }

    protected override void SetupApplication(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();
        app.MapControllers();
    }
}
