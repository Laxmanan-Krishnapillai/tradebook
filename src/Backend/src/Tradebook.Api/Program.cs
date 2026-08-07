using System.Text;
using System.Text.Json.Serialization;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Tradebook.Api;
using Tradebook.Core.Interfaces;
using Tradebook.Infrastructure.Caching;
using Tradebook.Infrastructure.Data;
using Tradebook.Infrastructure.Options;
using Tradebook.Api.RealTime;
using Tradebook.Core.Analytics;
using Tradebook.Api.Features.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolver = AppJsonSerializerContext.Default);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOptions<DatabaseOptions>().BindConfiguration("Database").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<INpgsqlConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddScoped<IDeliveryRepository, DeliveryRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddHybridCache();
builder.Services.AddSingleton<ICacheService, HybridCacheService>();
builder.Services.AddSingleton<SemanticModelLoader>();
builder.Services.AddSingleton<SemanticQueryCompiler>();
var signingKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "sub", RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (context.Request.Path.StartsWithSegments("/hubs"))
                context.Token = context.Request.Query["access_token"];
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy("ReadPolicy", policy => policy.RequireRole("Trader", "BackOffice", "Admin"));
    options.AddPolicy("TraderPolicy", policy => policy.RequireRole("Trader", "Admin"));
    options.AddPolicy("BackOfficePolicy", policy => policy.RequireRole("BackOffice", "Admin"));
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
});
builder.Services.AddTradebookHealthChecks();
builder.Services.AddFastEndpoints();
builder.Services.AddDashboardPush();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints(config => config.Serializer.Options.TypeInfoResolver = AppJsonSerializerContext.Default);
app.MapTradebookHealthEndpoints();
app.MapDashboardPushHub();

// SPA hosting (Task 02 §3.7): serve the built frontend; unmatched /api/* and /hubs/*
// return 404 instead of index.html.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("{*path:regex(^(?!api|hubs)(.*)$)}", "index.html");

app.Run();

public partial class Program;
