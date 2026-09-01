using Microsoft.AspNetCore.Authentication.JwtBearer;
using _808Music.Application;
using _808Music.Application.Abstractions;
using _808Music.Infrastructure;
using _808Music.Infrastructure.Persistence;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models.Mail;
using RS1_2024_25.API.Data.Models.Stripe;
using RS1_2024_25.API.Helper.Auth;
using RS1_2024_25.API.Hubs;
using RS1_2024_25.API.Services;
using RS1_2024_25.API.Services.Interfaces;
using RS1_2024_25.API.Services.Recommendations;
using System.Text;
using static RS1_2024_25.API.Endpoints.CityEndpoints.ProductGetAllEndpoint;
using FluentValidation;
using RS1_2024_25.API.Data.Models.Auth;
using FluentValidation.AspNetCore;
using Amazon.S3;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("db1")));

builder.Services.AddDbContext<MusicDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("db1"), sql =>
    {
        sql.MigrationsAssembly(typeof(MusicDbContext).Assembly.FullName);
        sql.MigrationsHistoryTable("__EFMigrationsHistory_808MusicClean");
    }));

builder.Services.AddScoped<IApplicationDbContext>(sp =>
    sp.GetRequiredService<MusicDbContext>());

builder.Services.AddControllers();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(x => {
    x.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "808 Music API",
        Version = "v1",
        Description = "Legacy API surface kept for backward compatibility. Existing routes remain available without a /v1 URL segment."
    });
    x.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "808 Music API",
        Version = "v2",
        Description = "Clean Architecture API surface for new and refactored 808 Music modules."
    });
    x.DocInclusionPredicate((documentName, apiDescription) =>
    {
        if (string.IsNullOrWhiteSpace(apiDescription.GroupName))
        {
            return documentName == "v1";
        }

        return string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase);
    });
    x.TagActionsBy(apiDescription => [GetSwaggerTag(apiDescription)]);
    x.OperationFilter<MyAuthorizationSwaggerHeader>();
    var security = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        In = ParameterLocation.Header,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT for authorization.",
    };
    x.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, security);

    x.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                        Id = JwtBearerDefaults.AuthenticationScheme
                }
            },
            []
        }
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(
    options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ClockSkew = TimeSpan.Zero
        };

        

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                // If the request is for our hub...
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/notificationsHub") || path.StartsWithSegments("/chatHub")))
                {
                    // Read the token out of the query string
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    }
);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin() 
              .AllowAnyMethod()  
              .AllowAnyHeader(); 
    });
});

builder.Services.AddSignalR();

builder.Services.AddStackExchangeRedisCache(opt =>
{
    opt.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Custom services
builder.Services.AddTransient<MyAuthService>();
builder.Services.AddTransient<IMyFileHandler,FileHandler>();
builder.Services.AddTransient<TokenProvider>();
builder.Services.AddSingleton<RecurringTaskExecutionCoordinator>();
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("StripeSettings"));
builder.Services.AddTransient<IMyMailService, MailService>();
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection(nameof(MailSettings)));
builder.Services.AddTransient<DeleteService>();
builder.Services.AddScoped<ILegacyPlaylistRecommendationReader, LegacyPlaylistRecommendationReader>();
builder.Services.AddHostedService<MyBackgroundService>();
builder.Services.Configure<CleanArchitectureBackgroundServiceOptions>(
    builder.Configuration.GetSection(CleanArchitectureBackgroundServiceOptions.SectionName));
builder.Services.AddHostedService<CleanArchitectureBackgroundService>();
builder.Services.AddSingleton<IMyCacheService, MyRedisCacheService>();
builder.Services.AddTransient<NotificationTransformerService>();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddEfCrudPersistence<MusicDbContext>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<MyAppUser>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigration");

    logger.LogInformation("Applying pending 808Music database migrations.");
    var dbContext = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
    await dbContext.Database.MigrateAsync();
    logger.LogInformation("Database migrations are up to date.");
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "808 Music API v1 (Legacy)");
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "808 Music API v2");
});

app.UseCors(
    options => options
        .SetIsOriginAllowed(x => _ = true)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
); //This needs to set everything allowed

app.UseCors("AllowAll"); // CORS should be used before static files

app.Use(async (context, next) =>
{
    context.Request.Path = NormalizeMediaRequestPath(context.Request.Path);
    await next();
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.WebRootPath)),
    RequestPath = "/media",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*"); 
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS"); 
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<NotificationsHub>("/notificationsHub");
app.MapHub<ChatHub>("/chatHub");

app.Run();
app.UseCors("AllowAll");

static string GetSwaggerTag(ApiDescription apiDescription)
{
    apiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerName);

    var versionLabel = string.Equals(apiDescription.GroupName, "v2", StringComparison.OrdinalIgnoreCase)
        ? "V2"
        : "Legacy";

    return $"{versionLabel} - {GetResourceName(controllerName)}";
}

static PathString NormalizeMediaRequestPath(PathString requestPath)
{
    const string mediaPrefix = "/media/";
    var path = requestPath.Value;

    if (string.IsNullOrEmpty(path) ||
        !path.StartsWith(mediaPrefix, StringComparison.OrdinalIgnoreCase))
    {
        return requestPath;
    }

    var segments = path[mediaPrefix.Length..]
        .Split('/', StringSplitOptions.RemoveEmptyEntries);

    if (segments.Length == 0)
    {
        return new PathString(mediaPrefix);
    }

    if (segments[0].Equals("images", StringComparison.OrdinalIgnoreCase))
    {
        segments[0] = "Images";
    }

    if (segments.Length > 1 && segments[0] == "Images")
    {
        segments[1] = segments[1].ToLowerInvariant() switch
        {
            "albumcovers" => "AlbumCovers",
            "artistbgs" => "ArtistBgs",
            "artistpfps" => "ArtistPfps",
            "playlists" => "Playlists",
            "profilepictures" => "ProfilePictures",
            "events" => "events",
            "logo" => "logo",
            "products" => "products",
            _ => segments[1]
        };
    }

    return new PathString(mediaPrefix + string.Join('/', segments));
}

static string GetResourceName(string? controllerName)
{
    if (string.IsNullOrWhiteSpace(controllerName))
    {
        return "API";
    }

    if (controllerName.Equals("AiPlaylists", StringComparison.OrdinalIgnoreCase))
    {
        return "AI Playlists";
    }

    if (controllerName.Contains("Recommendation", StringComparison.OrdinalIgnoreCase))
    {
        return "Recommendations";
    }

    if (controllerName.Contains("Stem", StringComparison.OrdinalIgnoreCase))
    {
        return "Stems";
    }

    if (controllerName.Contains("Track", StringComparison.OrdinalIgnoreCase))
    {
        return "Tracks";
    }

    if (controllerName.Contains("Artist", StringComparison.OrdinalIgnoreCase))
    {
        return "Artists";
    }

    if (controllerName.Contains("Product", StringComparison.OrdinalIgnoreCase))
    {
        return "Products";
    }

    return controllerName.EndsWith("Endpoint", StringComparison.OrdinalIgnoreCase)
        ? controllerName[..^"Endpoint".Length]
        : controllerName;
}
