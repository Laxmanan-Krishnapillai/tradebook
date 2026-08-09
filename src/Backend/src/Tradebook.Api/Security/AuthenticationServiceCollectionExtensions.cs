using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

namespace Tradebook.Api.Security;

public static class AuthenticationServiceCollectionExtensions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "MA0051:Method is too long",
        Justification = "Single cohesive auth wiring; scheduled for decomposition post-merge."
    )]
    public static IServiceCollection AddTradebookAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        services.AddSingleton<IValidateOptions<EntraOptions>, EntraOptionsValidator>();
        services
            .AddOptions<EntraOptions>()
            .Bind(configuration.GetRequiredSection(EntraOptions.SectionName))
            .ValidateOnStart();
        if (environment.IsEnvironment("Testing"))
            services
                .AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    null
                );
        else
            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddMicrosoftIdentityWebApi(
                    configuration.GetRequiredSection(EntraOptions.SectionName)
                );

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .PostConfigure<IOptions<EntraOptions>>(
                (bearer, configured) =>
                {
                    bearer.MapInboundClaims = false;
                    bearer.TokenValidationParameters.RoleClaimType = "roles";
                    var priorValidated = bearer.Events.OnTokenValidated;
                    var priorMessage = bearer.Events.OnMessageReceived;
                    bearer.Events.OnMessageReceived = async context =>
                    {
                        if (priorMessage is not null)
                            await priorMessage(context).ConfigureAwait(false);
                        if (
                            context.Request.Path.StartsWithSegments(
                                "/hubs",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                            context.Token = context.Request.Query["access_token"];
                    };
                    bearer.Events.OnTokenValidated = async context =>
                    {
                        if (priorValidated is not null)
                            await priorValidated(context).ConfigureAwait(false);
                        var tenant = configured.Value.TenantId;
                        if (
                            !string.Equals(
                                context.Principal?.FindFirst("tid")?.Value,
                                tenant,
                                StringComparison.Ordinal
                            ) || !Guid.TryParse(context.Principal?.FindFirst("oid")?.Value, out _)
                        )
                        {
                            context.Fail("A valid oid from the configured tenant is required.");
                            return;
                        }
                        ((ClaimsIdentity)context.Principal.Identity!).AddClaim(
                            new Claim("tradebook_tenant", tenant)
                        );
                    };
                }
            );

        services.AddAuthorization(options =>
        {
            static void Scope(AuthorizationPolicyBuilder policy) =>
                policy.RequireClaim("scp", "access_as_user");
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("scp", "access_as_user")
                .Build();
            options.AddPolicy(
                "ReadPolicy",
                policy =>
                {
                    Scope(policy);
                    policy.RequireRole("Trader", "BackOffice", "Admin");
                }
            );
            options.AddPolicy(
                "TraderPolicy",
                policy =>
                {
                    Scope(policy);
                    policy.RequireRole("Trader", "Admin");
                }
            );
            options.AddPolicy(
                "BackOfficePolicy",
                policy =>
                {
                    Scope(policy);
                    policy.RequireRole("BackOffice", "Admin");
                }
            );
            options.AddPolicy(
                "AdminPolicy",
                policy =>
                {
                    Scope(policy);
                    policy.RequireRole("Admin");
                }
            );
        });
        return services;
    }
}
