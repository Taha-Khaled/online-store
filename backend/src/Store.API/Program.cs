using Store.API.Binding;
using Store.API.Errors;
using Store.API.Extensions;
using Store.Application;
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

var app = builder.Build();

// Fail fast, before touching the database, when a JWT setting is missing or invalid (FR-009).
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(app.Environment.IsDevelopment());
}

app.UseExceptionHandler(_ => { });
app.UseStatusCodePages(ErrorResponseWriter.WriteStatusCodePageAsync);
app.UseHttpsRedirection();
app.UseProductImages();
app.MapControllers();

await app.RunAsync();
