using KekUploadServer.Database;
using KekUploadServer.Middlewares;
using KekUploadServer.Services;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddDbContext<UploadDataContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("KekUploadDb"));
        });

        builder.Services.AddControllers();
        builder.Services.AddScoped<IUploadService, UploadService>();
        builder.Services.AddScoped<IWebService, WebService>();
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddMemoryCache();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // Test database connection
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<UploadDataContext>();
        // Check if migrations are needed
        context.Database.Migrate();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}