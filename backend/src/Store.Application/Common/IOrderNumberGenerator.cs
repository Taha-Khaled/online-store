namespace Store.Application.Common;

/// <summary>Hands out unique, increasing order numbers (FR-020, research R4).</summary>
public interface IOrderNumberGenerator
{
    /// <summary>The next order number, e.g. <c>ORD-1001</c>. A number taken by a failed checkout is not reused.</summary>
    Task<string> NextAsync(CancellationToken cancellationToken = default);
}
