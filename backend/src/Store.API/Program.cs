using Store.API.Binding;
using Store.API.Errors;
using Store.Application;
using Store.Infrastructure;
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

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(app.Environment.IsDevelopment());
}

app.UseExceptionHandler(_ => { });
app.UseStatusCodePages(ErrorResponseWriter.WriteStatusCodePageAsync);
app.UseHttpsRedirection();
app.MapControllers();

await app.RunAsync();
