using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Store.Application.Common;

namespace Store.Infrastructure.Orders;

/// <summary>
/// Reads the next value of <c>dbo.OrderNumberSequence</c> (research R4). The interpolated
/// <c>SqlQuery</c> has no user input (constitution III). <c>ToListAsync</c> without other operators
/// keeps EF from wrapping the statement in a subquery, where SQL Server rejects NEXT VALUE FOR.
/// </summary>
public sealed class OrderNumberGenerator(IAppDbContext db) : IOrderNumberGenerator
{
    public const string Prefix = "ORD-";

    public async Task<string> NextAsync(CancellationToken cancellationToken = default)
    {
        var values = await db.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR dbo.OrderNumberSequence AS [Value]")
            .ToListAsync(cancellationToken);

        return Prefix + values.Single().ToString(CultureInfo.InvariantCulture);
    }
}
