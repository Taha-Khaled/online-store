using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Store.API.Extensions;

/// <summary>Swagger with a Bearer scheme on <c>[Authorize]</c> actions (FR-006, research R12). Development only.</summary>
public static class SwaggerExtensions
{
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Store API", Version = "v1" });
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Paste the access token returned by login, register or refresh."
            });
            options.OperationFilter<AuthorizeOperationFilter>();
        });

        return services;
    }

    public static WebApplication UseSwaggerInDevelopment(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }

    /// <summary>Adds the Bearer requirement to actions (or controllers) marked <c>[Authorize]</c>.</summary>
    private sealed class AuthorizeOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var method = context.MethodInfo;
            var requiresAuth = method.GetCustomAttributes<AuthorizeAttribute>(true).Any()
                || (method.DeclaringType?.GetCustomAttributes<AuthorizeAttribute>(true).Any() ?? false);
            if (!requiresAuth)
            {
                return;
            }

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = []
            });
        }
    }
}
