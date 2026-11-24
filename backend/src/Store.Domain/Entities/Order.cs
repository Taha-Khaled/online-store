using Store.Domain.Enums;

namespace Store.Domain.Entities;

public class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public int Id { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public int UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal ShippingFee { get; private set; }
    public decimal Total { get; private set; }
    public string ShippingName { get; private set; } = null!;
    public string ShippingPhone { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string AddressLine { get; private set; } = null!;
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public User User { get; private set; } = null!;
    public IReadOnlyCollection<OrderItem> Items => _items;

    /// <summary>
    /// Creates an order that already happened (seed data or imported history), in any status.
    /// New customer orders and status transitions belong to the Orders feature.
    /// </summary>
    public static Order CreateHistorical(
        string orderNumber,
        int userId,
        OrderStatus status,
        decimal subtotal,
        decimal shippingFee,
        decimal total,
        string shippingName,
        string shippingPhone,
        string city,
        string addressLine,
        string? notes,
        DateTime createdAt,
        IEnumerable<OrderItem> items)
    {
        var order = new Order
        {
            OrderNumber = orderNumber,
            UserId = userId,
            Status = status,
            Subtotal = subtotal,
            ShippingFee = shippingFee,
            Total = total,
            ShippingName = shippingName,
            ShippingPhone = shippingPhone,
            City = city,
            AddressLine = addressLine,
            Notes = notes,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
        order._items.AddRange(items);
        return order;
    }
}
