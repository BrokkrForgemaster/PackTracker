using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PackTracker.Api.Middleware;
using PackTracker.Infrastructure;

namespace PackTracker.Presentation;

public class ApiHostedService : IHostedService
{
    private IHost? _apiHost;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _apiHost = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://localhost:5001");

                webBuilder.ConfigureServices(services =>
                {
                    // 🔹 Infrastructure: registers ILoggingService<> + others
                    services.AddInfrastructure();

                    // 🔹 API services
                    services.AddControllers();
                    services.AddEndpointsApiExplorer();
                    services.AddSwaggerGen(c =>
                    {
                        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                        {
                            Title = "🐺 PackTracker API",
                            Version = "v1",
                            Description = "API powering PackTracker — House Wolf’s system for Star Citizen logistics, ops, and data.",
                            Contact = new Microsoft.OpenApi.Models.OpenApiContact
                            {
                                Name = "House Wolf",
                                Url = new Uri("https://housewolf.co"),
                            },
                            
                        });
                        var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                        var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{asmName}.xml");
                        if (File.Exists(xmlPath))
                        {
                            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                        }
                    });
                   
                    services.AddHealthChecks();
                });

                webBuilder.Configure(app =>
                {
                    app.UseStaticFiles();

                    app.UseMiddleware<RequestLoggingMiddleware>();
                    app.UseMiddleware<ExceptionHandlingMiddleware>();

                    app.UseSwagger();
                    app.UseSwaggerUI(c =>
                    {
                        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PackTracker API v1");
                        c.RoutePrefix = "swagger";
                        c.DocumentTitle = "🐺 PackTracker API Docs";
                        c.InjectStylesheet("/swagger-ui/custom.css");
                        c.InjectJavascript("/swagger-ui/custom.js");
                    });

                    app.UseHttpsRedirection();
                });
            })
            .Build();

        return _apiHost.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _apiHost?.StopAsync(cancellationToken) ?? Task.CompletedTask;
}
