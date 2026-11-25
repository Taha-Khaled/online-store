using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Store.Infrastructure.Seeding;

namespace Store.API.Extensions;

public static class ProductImagesExtensions
{
    public const string RequestPath = "/images/products";

    /// <summary>
    /// Serves the seed images folder publicly at <c>/images/products</c> (FR-007, research R11).
    /// No directory browsing; paths outside the folder are never served; missing files fall
    /// through to the 404 error shape.
    /// </summary>
    public static WebApplication UseProductImages(this WebApplication app)
    {
        var imagesPath = app.Services.GetRequiredService<IOptions<SeedOptions>>().Value.ImagesPath
            ?? throw new InvalidOperationException("The product images folder ('Seed:ImagesPath') is not configured.");

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(imagesPath),
            RequestPath = RequestPath
        });

        return app;
    }
}
