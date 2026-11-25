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
