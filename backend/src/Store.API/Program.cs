using Store.API.Authorization;
using Store.API.Binding;
using Store.API.Errors;
using Store.API.Extensions;
using Store.Application;
using Store.Application.Common;
using Store.Infrastructure;
using Microsoft.Extensions.Options;
using Store.Infrastructure.Security;
using Store.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.PostConfigure<SeedOptions>(options =>
    options.ImagesPath ??= Path.Combine(builder.Environment.ContentRootPath, "SeedImages"));

builder.Services
    .AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        options.ModelBinderProviders.Insert(0, new ProductSortModelBinderProvider());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressMapClientErrors = true;
        options.InvalidModelStateResponseFactory = ModelStateErrors.CreateResponse;
    });
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddJwtAuthentication();
builder.Services.AddStorePolicies();
builder.Services.AddSwagger();

var app = builder.Build();

// Fail fast, before touching the database, when a required setting is missing or invalid.
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
_ = app.Services.GetRequiredService<IOptions<ShopOptions>>().Value;

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(app.Environment.IsDevelopment());
}

app.UseExceptionHandler(_ => { });
app.UseStatusCodePages(ErrorResponseWriter.WriteStatusCodePageAsync);
app.UseHttpsRedirection();
app.UseProductImages();
app.UseSwaggerInDevelopment();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
