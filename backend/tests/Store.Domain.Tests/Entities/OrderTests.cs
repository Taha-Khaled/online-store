using Shouldly;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Domain.Tests.Entities;

public class OrderTests
{
    private static readonly DateTime CreatedAt = new(2026, 10, 27, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateHistorical_SetsAllFieldsAndUpdatedAtEqualsCreatedAt()
    {
        var order = CreateDeliveredOrder([]);

        order.OrderNumber.ShouldBe("ORD-0001");
        order.UserId.ShouldBe(2);
        order.Status.ShouldBe(OrderStatus.Delivered);
        order.Subtotal.ShouldBe(1800m);
        order.ShippingFee.ShouldBe(50m);
        order.Total.ShouldBe(1850m);
        order.ShippingName.ShouldBe("Demo Customer");
        order.ShippingPhone.ShouldBe("01000000000");
        order.City.ShouldBe("Cairo");
        order.AddressLine.ShouldBe("1 Demo Street");
        order.Notes.ShouldBeNull();
        order.CreatedAt.ShouldBe(CreatedAt);
        order.UpdatedAt.ShouldBe(CreatedAt);
    }

    [Fact]
    public void CreateHistorical_AttachesAllItemsInOrder()
    {
        var jeans = new OrderItem(10, "Classic Denim Jeans", "Blue", "M", 1100m, 1);
        var tShirt = new OrderItem(20, "Classic Cotton T-Shirt", "White", "M", 350m, 2);

        var order = CreateDeliveredOrder([jeans, tShirt]);

        order.Items.ShouldBe([jeans, tShirt]);
    }

    [Fact]
    public void CreateHistorical_WithNoItems_HasEmptyItems()
    {
        var order = CreateDeliveredOrder([]);

        order.Items.ShouldBeEmpty();
    }

    [Fact]
    public void CreatePending_StartsPendingWithTotalsFromItemsAndFee()
    {
        var now = new DateTime(2026, 11, 24, 12, 0, 0, DateTimeKind.Utc);
        var top = new OrderItem(87, "Basic Ribbed Top", "White", "S", 300m, 2);
        var tee = new OrderItem(12, "Classic Cotton T-Shirt", "Black", "M", 350m, 1);

        var order = Order.CreatePending("ORD-1001", 5, Shipping, [top, tee], 50m, now);

        order.OrderNumber.ShouldBe("ORD-1001");
        order.UserId.ShouldBe(5);
        order.Status.ShouldBe(OrderStatus.Pending);
        order.Subtotal.ShouldBe(950m);
        order.ShippingFee.ShouldBe(50m);
        order.Total.ShouldBe(1000m);
        order.ShippingName.ShouldBe("Test Customer");
        order.ShippingPhone.ShouldBe("+201000000000");
        order.City.ShouldBe("Giza");
        order.AddressLine.ShouldBe("5 Nile Street");
        order.Notes.ShouldBe("Ring twice");
        order.CreatedAt.ShouldBe(now);
        order.UpdatedAt.ShouldBe(now);
        order.Items.ShouldBe([top, tee]);
    }

    [Fact]
    public void CreatePending_ZeroFee_TotalEqualsSubtotal()
    {
        var order = Order.CreatePending("ORD-1002", 5, Shipping, [Item()], 0m, CreatedAt);

        order.Total.ShouldBe(order.Subtotal);
    }

    [Theory]
    [InlineData("", 1, 50)]
    [InlineData(" ", 1, 50)]
    [InlineData("ORD-1003", 0, 50)]
    [InlineData("ORD-1003", 1, -1)]
    public void CreatePending_InvalidInput_Throws(string orderNumber, int itemCount, int shippingFee)
    {
        var items = Enumerable.Range(0, itemCount).Select(_ => Item()).ToList();

        Should.Throw<DomainException>(() => Order.CreatePending(orderNumber, 5, Shipping, items, shippingFee, CreatedAt));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    public void Cancel_FromPendingOrConfirmed_CancelsAndUpdatesTime(OrderStatus status)
    {
        var order = CreateOrder(status);
        var later = CreatedAt.AddHours(3);

        order.Cancel(later);

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.UpdatedAt.ShouldBe(later);
        order.CreatedAt.ShouldBe(CreatedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void Cancel_FromOtherStatuses_ThrowsAndKeepsTheOrder(OrderStatus status)
    {
        var order = CreateOrder(status);

        Should.Throw<DomainException>(() => order.Cancel(CreatedAt.AddHours(3)));

        order.Status.ShouldBe(status);
        order.UpdatedAt.ShouldBe(CreatedAt);
    }

    private static readonly ShippingDetails Shipping = new("Test Customer", "+201000000000", "Giza", "5 Nile Street", "Ring twice");

    private static OrderItem Item() => new(87, "Basic Ribbed Top", "White", "S", 300m, 1);

    private static Order CreateOrder(OrderStatus status) =>
        Order.CreateHistorical(
            "ORD-0009", 2, status, 300m, 50m, 350m, "Demo Customer", "01000000000", "Cairo", "1 Demo Street", null, CreatedAt, [Item()]);

    private static Order CreateDeliveredOrder(IEnumerable<OrderItem> items) =>
        Order.CreateHistorical(
            "ORD-0001",
            2,
            OrderStatus.Delivered,
            1800m,
            50m,
            1850m,
            "Demo Customer",
            "01000000000",
            "Cairo",
            "1 Demo Street",
            null,
            CreatedAt,
            items);
}
