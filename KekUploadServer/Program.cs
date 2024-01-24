using KekUploadServer.Database;
using KekUploadServer.Middlewares;
using KekUploadServer.Plugins;
using KekUploadServer.Services;
using Microsoft.EntityFrameworkCore;

namespace KekUploadServer;

internal class Program
{
    private const string Version = "1.2.0";
    
    private static async Task Main(string[] args)
    {
        Console.WriteLine("░█░█░█▀▀░█░█░█░█░█▀█░█░░░█▀█░█▀█░█▀▄░█▀▀░█▀▀░█▀▄░█░█░█▀▀░█▀▄░░░█▀▀░▄█▄█▄\n" +
                          "░█▀▄░█▀▀░█▀▄░█░█░█▀▀░█░░░█░█░█▀█░█░█░▀▀█░█▀▀░█▀▄░▀▄▀░█▀▀░█▀▄░░░█░░░▄█▄█▄\n" +
                          "░▀░▀░▀▀▀░▀░▀░▀▀▀░▀░░░▀▀▀░▀▀▀░▀░▀░▀▀░░▀▀▀░▀▀▀░▀░▀░░▀░░▀▀▀░▀░▀░░░▀▀▀░░▀░▀░");
        
        Console.WriteLine($"Running KekUploadServer Version {Version}");
        
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<UploadDataContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("KekUploadDb"));
        });

        builder.Services.AddControllers();
        builder.Services.AddSingleton<IUploadService, UploadService>();
        builder.Services.AddTransient<IWebService, WebService>();
        builder.Services.AddTransient<IMediaService, MediaService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseWebSockets();

        // Load plugins
        var pluginLoader = new PluginLoader();
        var pluginLogger = app.Services.GetRequiredService<ILogger<PluginLoader>>();
        await pluginLoader.LoadPlugins(app, pluginLogger);

        // forward logging to plugins
        app.Services.GetRequiredService<ILoggerFactory>().AddProvider(new PluginLoggerProvider());

        // Test database connection
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<UploadDataContext>();
        // Check if migrations are needed
        await context.Database.MigrateAsync();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        // Start plugins
        await pluginLoader.StartPlugins();

        // Register shutdown hook
        var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                pluginLoader.StopPlugins().Wait();
            }
            catch (Exception e)
            {
                switch (e)
                {
                    case AggregateException ae:
                        ae.Handle(innerE =>
                        {
                            pluginLogger.LogError(innerE, "Error while stopping plugins");
                            return true;
                        });
                        break;
                    case TaskCanceledException tc:
                        pluginLogger.LogError(tc.InnerException ?? tc, "Error while stopping plugins");
                        break;
                    default:
                        pluginLogger.LogError(e, "Error while stopping plugins");
                        break;
                }
            }
        });

        await app.RunAsync();
    }
}