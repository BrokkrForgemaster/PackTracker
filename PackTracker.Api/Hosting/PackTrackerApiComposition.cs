using PackTracker.Api.Hubs;
using PackTracker.Application;
using PackTracker.Api.Services;
using PackTracker.Api.Middleware;
using PackTracker.Domain.Security;
using PackTracker.Api.Controllers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Api.Authentication;
using PackTracker.Application.Options;
using Microsoft.AspNetCore.HttpOverrides;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Services;
using PackTracker.Infrastructure.ApiHosting;
using PackTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.OAuth;
using PackTracker.Infrastructure.Services.Admin;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace PackTracker.Api.Hosting;

/// <summary name="PackTrackerApiComposition">
/// Central composition root for the PackTracker API application. This class is responsible for configuring
/// services, middleware, and application initialization logic. It serves as the main entry point for setting up
/// the API host, ensuring that all necessary components are registered and configured in a cohesive manner.
/// </summary>
public static class PackTrackerApiComposition
{
    public static void ConfigureServices(
        WebApplicationBuilder builder,
        ISettingsService settingsService,
        bool isEmbeddedHost)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddApplication();
        builder.Services.AddSingleton<IUserIdProvider, DiscordIdUserIdProvider>();
        builder.Services.AddScoped<ICurrentUserService, HttpContextCurrentUserService>();
        builder.Services.AddScoped<IAssistanceRequestNotifier, SignalRAssistanceRequestNotifier>();
        builder.Services.AddScoped<IAuthWorkflowService, AuthWorkflowService>();
        builder.Services.AddScoped<ICraftingWorkflowNotifier, SignalRCraftingWorkflowNotifier>();
        builder.Services.AddScoped<IRequestTicketNotifier, SignalRRequestTicketNotifier>();

        var securityOptions = builder.Configuration.GetSection(SecurityOptions.Section).Get<SecurityOptions>() ?? new();

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            if (securityOptions.TrustedProxies.Count > 0)
            {
                foreach (var proxy in securityOptions.TrustedProxies)
                {
                    if (System.Net.IPAddress.TryParse(proxy, out var address))
                    {
                        options.KnownProxies.Add(address);
                    }
                }
            }
            else
            {
                // No specific proxy IPs configured — trust all upstream headers.
                // Required for PaaS deployments (Render, Railway, Fly.io) where proxy
                // IPs are ephemeral. Without this, X-Forwarded-Proto is ignored,
                // Request.Scheme stays "http", and the OAuth code-exchange builds a
                // redirect_uri Discord rejects as mismatched.
                options.KnownNetworks.Add(new IPNetwork(System.Net.IPAddress.Any, 0));
                options.KnownNetworks.Add(new IPNetwork(System.Net.IPAddress.IPv6Any, 0));
            }
        });

        builder.Services.Configure<StartupOptions>(
            builder.Configuration.GetSection(StartupOptions.SectionName));

        builder.Services.AddCors(cors =>
        {
            cors.AddPolicy("PackTrackerDefault", policy =>
            {
                if (securityOptions.AllowedCorsOrigins.Count > 0)
                {
                    policy.WithOrigins(securityOptions.AllowedCorsOrigins.ToArray())
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
                else
                {
                    policy.AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowAnyOrigin();
                }
            });
        });

        builder.Services.AddPackTrackerApiHost(settingsService, options =>
        {
            options.SmartScheme = ApiAuthenticationDefaults.SmartScheme;
            options.CookieScheme = ApiAuthenticationDefaults.CookieScheme;
            options.DiscordScheme = ApiAuthenticationDefaults.DiscordScheme;
            options.CookieSecurePolicy = isEmbeddedHost ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
            options.SelectScheme = ApiAuthenticationDefaults.SelectScheme;
            options.GetSignalRAccessToken = ApiAuthenticationDefaults.GetSignalRAccessToken;
            options.ConfigureControllers = mvc =>
                mvc.AddApplicationPart(typeof(ProfilesController).Assembly);
            options.ConfigureDiscordEvents = events => ConfigureDiscordEvents(events);
        });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPolicyNames.AdminAccess, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SecurityConstants.AdminEligibleRoles.ToArray());
            });
        });

        builder.Services.AddSwaggerGen();
    }

    public static void ConfigurePipeline(
        WebApplication app,
        bool useHttpsRedirection,
        bool enableSwaggerUi)
    {
        app.UseForwardedHeaders();
        app.UseCors("PackTrackerDefault");

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<ValidationHandlingMiddleware>();

        if (enableSwaggerUi && app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (useHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<RequestsHub>(RequestsHub.Route);
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready")
        });
    }

    public static async Task InitializeDatabaseAsync(
        WebApplication app,
        CancellationToken cancellationToken)
    {
        using var scope = app.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        var maintenance = scope.ServiceProvider.GetRequiredService<IDataMaintenanceService>();
        var seedService = scope.ServiceProvider.GetRequiredService<CraftingSeedService>();
        var startupState = scope.ServiceProvider.GetRequiredService<IStartupInitializationState>();
        var adminSeedService = scope.ServiceProvider.GetRequiredService<AdminSeedService>();
        var startupOptions = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<StartupOptions>>()
            .Value;

        // Defensive: ensure critical auth tables exist even if EF migrations fail.
        // These tables have no prior migration and will be absent on an existing DB.
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""LoginStates"" (
                    ""Id"" uuid NOT NULL,
                    ""ClientState"" character varying(256) NOT NULL,
                    ""AccessToken"" text NOT NULL DEFAULT '',
                    ""RefreshToken"" text NOT NULL DEFAULT '',
                    ""ExpiresIn"" integer NOT NULL DEFAULT 0,
                    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""ExpiresAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_LoginStates"" PRIMARY KEY (""Id"")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LoginStates_ClientState""
                    ON ""LoginStates""(""ClientState"");

                CREATE TABLE IF NOT EXISTS ""SyncMetadatas"" (
                    ""Id"" uuid NOT NULL,
                    ""TaskName"" character varying(128) NOT NULL,
                    ""LastStartedAt"" timestamp with time zone,
                    ""LastCompletedAt"" timestamp with time zone,
                    ""IsSuccess"" boolean NOT NULL DEFAULT false,
                    ""LastErrorMessage"" text,
                    ""ItemsProcessed"" integer NOT NULL DEFAULT 0,
                    CONSTRAINT ""PK_SyncMetadatas"" PRIMARY KEY (""Id"")
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SyncMetadatas_TaskName""
                    ON ""SyncMetadatas""(""TaskName"");

                CREATE TABLE IF NOT EXISTS ""DistributedLocks"" (
                    ""LockKey"" character varying(128) NOT NULL,
                    ""LockedBy"" character varying(128) NOT NULL,
                    ""LockedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    ""ExpiresAt"" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_DistributedLocks"" PRIMARY KEY (""LockKey"")
                );
            ", cancellationToken);
            logger.LogInformation("Critical auth tables verified.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not verify critical auth tables (non-fatal).");
        }

        try
        {
            logger.LogInformation("Ensuring showcase and medals columns exist...");
            await db.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Profiles""
                    ADD COLUMN IF NOT EXISTS ""ShowcaseBio""       character varying(5000),
                    ADD COLUMN IF NOT EXISTS ""ShowcaseEyebrow""   character varying(100),
                    ADD COLUMN IF NOT EXISTS ""ShowcaseImageUrl""  character varying(1024),
                    ADD COLUMN IF NOT EXISTS ""ShowcaseTagline""   character varying(200);

                CREATE TABLE IF NOT EXISTS ""MedalDefinitions"" (
                    ""Id""            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    ""Name""          character varying(200)   NOT NULL,
                    ""Description""   character varying(4000)  NOT NULL,
                    ""ImagePath""     character varying(512),
                    ""SourceSystem""  character varying(100)   NOT NULL,
                    ""DisplayOrder""  integer                  NOT NULL,
                    ""CreatedAt""     timestamp with time zone NOT NULL DEFAULT now(),
                    ""UpdatedAt""     timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT ""PK_MedalDefinitions"" PRIMARY KEY (""Id"")
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_MedalDefinitions_Name""
                    ON ""MedalDefinitions"" (""Name"");

                CREATE TABLE IF NOT EXISTS ""MedalAwards"" (
                    ""Id""                  uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    ""MedalDefinitionId""   uuid                     NOT NULL,
                    ""ProfileId""           uuid,
                    ""RecipientName""       character varying(200)   NOT NULL,
                    ""AwardedAt""           timestamp with time zone,
                    ""ImportedAt""          timestamp with time zone NOT NULL DEFAULT now(),
                    ""SourceSystem""        character varying(100)   NOT NULL,
                    ""Citation""            character varying(4000),
                    ""AwardedBy""           character varying(200),
                    CONSTRAINT ""PK_MedalAwards"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_MedalAwards_MedalDefinitions_MedalDefinitionId""
                        FOREIGN KEY (""MedalDefinitionId"") REFERENCES ""MedalDefinitions"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_MedalAwards_Profiles_ProfileId""
                        FOREIGN KEY (""ProfileId"") REFERENCES ""Profiles"" (""Id"") ON DELETE SET NULL
                );

                CREATE INDEX IF NOT EXISTS ""IX_MedalAwards_MedalDefinitionId_RecipientName""
                    ON ""MedalAwards"" (""MedalDefinitionId"", ""RecipientName"");

                CREATE INDEX IF NOT EXISTS ""IX_MedalAwards_ProfileId""
                    ON ""MedalAwards"" (""ProfileId"");

                ALTER TABLE ""MedalDefinitions""
                    ADD COLUMN IF NOT EXISTS ""AwardType"" character varying(50) NOT NULL DEFAULT 'Medal',
                    ADD COLUMN IF NOT EXISTS ""PublicImageUrl"" character varying(1024);

                ALTER TABLE ""MedalAwards""
                    ADD COLUMN IF NOT EXISTS ""AwardType"" character varying(50) NOT NULL DEFAULT 'Medal';
            ", cancellationToken);
            logger.LogInformation("Schema pre-check complete.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Schema pre-check encountered an issue (non-fatal, continuing).");
        }

        try
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync(cancellationToken);

            await maintenance.PerformDataMaintenanceAsync(cancellationToken);

            var preferredPath = Path.GetFullPath(
                Path.Combine(app.Environment.ContentRootPath, "..", "scunpacked-data", "blueprints.json"));

            var fallbackPath = Path.Combine(
                app.Environment.ContentRootPath,
                "..",
                "PackTracker.Presentation",
                "wwwroot",
                "data",
                "crafting-seed.json");

            var seedPath = File.Exists(preferredPath) ? preferredPath : fallbackPath;

            logger.LogInformation("Seeding crafting data from {Path}", seedPath);
            await seedService.SeedAsync(seedPath, cancellationToken);
            await adminSeedService.SeedAsync(cancellationToken);
            logger.LogInformation("Data seeding completed");
            startupState.MarkSucceeded();
        }
        catch (Exception ex)
        {
            startupState.MarkFailed(ex.Message);
            if (startupOptions.FailOnDatabaseInitializationError)
            {
                logger.LogCritical(
                    ex,
                    "Database initialization failed and strict startup policy is enabled. Startup will be aborted.");
                throw;
            }

            logger.LogWarning(
                ex,
                "Database initialization encountered issues. Startup is continuing in degraded mode because strict startup policy is disabled.");
        }
    }

    public static void ConfigureDiscordEvents(OAuthEvents events)
    {
        events.OnRedirectToAuthorizationEndpoint = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggingService<Program>>();

            logger.LogInformation(
                "Discord auth redirect. Scheme={Scheme} Host={Host}",
                context.HttpContext.Request.Scheme,
                context.HttpContext.Request.Host);

            var authUri = new Uri(context.RedirectUri);
            var fixedParts = authUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(part =>
            {
                if (!part.StartsWith("redirect_uri=", StringComparison.OrdinalIgnoreCase))
                {
                    return part;
                }

                var decoded = Uri.UnescapeDataString(part["redirect_uri=".Length..]);
                var callbackUri = new UriBuilder(decoded);

                if (callbackUri.Scheme == "http" && !callbackUri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    callbackUri.Scheme = "https";
                }

                if (callbackUri.Port != -1 && callbackUri.Port is 80 or 443 or 8080 or 10000 or 5199)
                {
                    callbackUri.Port = -1;
                }

                var fixedUri = callbackUri.Uri.ToString();
                logger.LogInformation("Discord redirect_uri fixed to: {Uri}", fixedUri);
                return "redirect_uri=" + Uri.EscapeDataString(fixedUri);
            });

            var newUri = new UriBuilder(authUri)
            {
                Query = string.Join("&", fixedParts)
            }.Uri.AbsoluteUri;

            context.Response.Redirect(newUri);
            return Task.CompletedTask;
        };

        events.OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggingService<Program>>();

            var message = context.Failure?.Message ?? "unknown";

            logger.LogError(
                context.Failure ?? new InvalidOperationException("Unknown remote failure"),
                "Discord remote failure: {Message}",
                message);

            var safeMessage = System.Net.WebUtility.HtmlEncode(message);
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = 200;
            var html = $"""
                <html>
                  <body style="background:#121212;color:#fff;font-family:sans-serif;text-align:center;padding-top:15%">
                    <h2>Discord Login Failed</h2>
                    <p style="color:#aaa;max-width:480px;margin:16px auto">{safeMessage}</p>
                    <p style="color:#888;font-size:13px">Close this tab and try logging in again.</p>
                    <button style="padding:10px 20px;border:none;border-radius:6px;background:#c2a23a;color:#000;font-weight:bold;margin-top:20px;" onclick="window.close()">Close</button>
                  </body>
                </html>
                """;
            context.HandleResponse();
            return context.Response.WriteAsync(html);
        };
    }
}
