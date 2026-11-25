using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates dbo.sp_GetDashboardStats per specs/001-database/contracts/sp_GetDashboardStats.md:
    /// orders in [@FromUtc, @ToUtc); one row, one column [Value] with a single JSON object.
    /// </summary>
    public partial class AddDashboardStatsProcedure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE PROCEDURE dbo.sp_GetDashboardStats
                    @FromUtc datetime2,
                    @ToUtc datetime2,
                    @LowStockThreshold int = 3
                AS
                BEGIN
                    SET NOCOUNT ON;

                    DECLARE @json nvarchar(max) =
                    (
                        SELECT
                            ISNULL((
                                SELECT SUM(o.Total)
                                FROM dbo.Orders o
                                WHERE o.Status = N'Delivered'
                                  AND o.CreatedAt >= @FromUtc AND o.CreatedAt < @ToUtc
                            ), 0) AS totalRevenue,
                            (
                                SELECT COUNT(*)
                                FROM dbo.Orders o
                                WHERE o.CreatedAt >= @FromUtc AND o.CreatedAt < @ToUtc
                            ) AS totalOrders,
                            JSON_QUERY((
                                SELECT
                                    COUNT(CASE WHEN o.Status = N'Pending' THEN 1 END) AS pending,
                                    COUNT(CASE WHEN o.Status = N'Confirmed' THEN 1 END) AS confirmed,
                                    COUNT(CASE WHEN o.Status = N'Shipped' THEN 1 END) AS shipped,
                                    COUNT(CASE WHEN o.Status = N'Delivered' THEN 1 END) AS delivered,
                                    COUNT(CASE WHEN o.Status = N'Cancelled' THEN 1 END) AS cancelled
                                FROM dbo.Orders o
                                WHERE o.CreatedAt >= @FromUtc AND o.CreatedAt < @ToUtc
                                FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                            )) AS ordersByStatus,
                            JSON_QUERY(ISNULL((
                                SELECT TOP (5)
                                    p.Id AS productId,
                                    p.Name AS productName,
                                    SUM(oi.Quantity) AS unitsSold
                                FROM dbo.OrderItems oi
                                JOIN dbo.Orders o ON o.Id = oi.OrderId
                                JOIN dbo.ProductVariants v ON v.Id = oi.ProductVariantId
                                JOIN dbo.Products p ON p.Id = v.ProductId
                                WHERE o.Status <> N'Cancelled'
                                  AND o.CreatedAt >= @FromUtc AND o.CreatedAt < @ToUtc
                                GROUP BY p.Id, p.Name
                                ORDER BY SUM(oi.Quantity) DESC, p.Name ASC
                                FOR JSON PATH
                            ), N'[]')) AS bestSellers,
                            JSON_QUERY(ISNULL((
                                SELECT
                                    v.Id AS variantId,
                                    p.Name AS productName,
                                    c.Name AS colorName,
                                    s.Name AS sizeName,
                                    v.Sku AS sku,
                                    v.Stock AS stock
                                FROM dbo.ProductVariants v
                                JOIN dbo.Products p ON p.Id = v.ProductId
                                JOIN dbo.Colors c ON c.Id = v.ColorId
                                JOIN dbo.Sizes s ON s.Id = v.SizeId
                                WHERE v.Stock BETWEEN 1 AND @LowStockThreshold
                                ORDER BY v.Stock ASC, p.Name ASC, c.Name ASC, s.DisplayOrder ASC
                                FOR JSON PATH
                            ), N'[]')) AS lowStockVariants
                        FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                    );

                    SELECT @json AS [Value];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_GetDashboardStats;");
        }
    }
}
