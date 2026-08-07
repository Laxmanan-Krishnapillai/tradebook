using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Tradebook.Api.Security;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddTradebookAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetRequiredSection(JwtOptions.SectionName))
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, configured) =>
            {
                var jwt = configured.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/hubs"))
                            context.Token = context.Request.Query["access_token"];
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        if (!Guid.TryParse(subject, out _))
                        {
                            context.Fail("JWT sub must be a UUID.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
            options.AddPolicy("ReadPolicy", policy => policy.RequireRole("Trader", "BackOffice", "Admin"));
            options.AddPolicy("TraderPolicy", policy => policy.RequireRole("Trader", "Admin"));
            options.AddPolicy("BackOfficePolicy", policy => policy.RequireRole("BackOffice", "Admin"));
            options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
        });

        return services;
    }
}
