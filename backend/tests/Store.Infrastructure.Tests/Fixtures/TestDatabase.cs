using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Store.Infrastructure.Persistence;

namespace Store.Infrastructure.Tests.Fixtures;

/// <summary>
/// One uniquely named database on the local SQL Server per instance (research R1).
/// The server comes from the STORE_TEST_SQLSERVER environment variable; the database is dropped
/// on dispose.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    public const string ServerVariable = "STORE_TEST_SQLSERVER";

    public TestDatabase()
    {
        var server = Environment.GetEnvironmentVariable(ServerVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                $"Environment variable '{ServerVariable}' is not set. Set it to a SQL Server connection string without a database name.");
        }

        DatabaseName = $"StoreTests_{Guid.NewGuid():N}";
        ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = DatabaseName }.ConnectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }
}
