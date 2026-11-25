using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Store.Infrastructure.Persistence.Converters;

/// <summary>
/// Dates are stored in UTC. SQL Server's datetime2 keeps no time zone, so values read back are
/// marked as UTC; values written are stored unchanged.
/// </summary>
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
