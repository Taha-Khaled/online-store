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

    /// <summary>Concurrency token: a status change made elsewhere makes a stale update fail (research R6).</summary>
    public byte[] RowVersion { get; private set; } = [];

    public User User { get; private set; } = null!;
    public IReadOnlyCollection<OrderItem> Items => _items;

    /// <summary>
    /// The only way to create a new customer order: status Pending, subtotal and total computed
    /// from the item snapshots and the shipping fee, created = updated (data-model.md "Changed: Order").
    /// </summary>
    public static Order CreatePending(
        string orderNumber,
        int userId,
        ShippingDetails shipping,
        IReadOnlyCollection<OrderItem> items,
        decimal shippingFee,
        DateTime now)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            throw new DomainException("An order needs an order number.");
        }

        if (items.Count == 0)
        {
            throw new DomainException("An order needs at least one item.");
        }

        if (shippingFee < 0)
        {
            throw new DomainException("The shipping fee cannot be negative.");
        }

        var subtotal = items.Sum(item => item.UnitPrice * item.Quantity);
        return CreateHistorical(
            orderNumber,
            userId,
            OrderStatus.Pending,
            subtotal,
            shippingFee,
            subtotal + shippingFee,
            shipping.Name,
            shipping.Phone,
            shipping.City,
            shipping.AddressLine,
            shipping.Notes,
            now,
            items);
    }

    /// <summary>
    /// Cancels a Pending or Confirmed order (domain rule). Whether a customer may cancel is decided
    /// by the application (Pending only).
    /// </summary>
    public void Cancel(DateTime now)
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.Confirmed))
        {
            throw new DomainException($"An order in status {Status} cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>
    /// Creates an order that already happened (seed data or imported history), in any status.
    /// New customer orders use <see cref="CreatePending"/>.
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
