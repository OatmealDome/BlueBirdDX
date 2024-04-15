using BlueBirdDX.WebApp.Models;
using BlueBirdDX.WebApp.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
IServiceCollection services = builder.Services;

services.Configure<DatabaseSettings>(builder.Configuration.GetSection(nameof(DatabaseSettings)));
services.AddSingleton<DatabaseSettings>(sp => sp.GetRequiredService<IOptions<DatabaseSettings>>().Value);
services.AddSingleton<DatabaseService>();

services.Configure<RemoteStorageSettings>(builder.Configuration.GetSection(nameof(RemoteStorageSettings)));
services.AddSingleton<RemoteStorageSettings>(sp => sp.GetRequiredService<IOptions<RemoteStorageSettings>>().Value);
services.AddSingleton<RemoteStorageService>();

services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.Run();