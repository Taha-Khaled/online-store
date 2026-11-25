using Store.Infrastructure;
using Store.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.PostConfigure<SeedOptions>(options =>
    options.ImagesPath ??= Path.Combine(builder.Environment.ContentRootPath, "SeedImages"));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync(app.Environment.IsDevelopment());
}

app.UseHttpsRedirection();

await app.RunAsync();
