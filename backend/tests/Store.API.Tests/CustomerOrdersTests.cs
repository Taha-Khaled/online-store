using System.Net;
using Shouldly;
using Store.API.Tests.Fixtures;

namespace Store.API.Tests;

/// <summary>US3-1/2, AC 10 (FR-021–FR-023, research R12). Read-only on the seeded orders.</summary>
public class CustomerOrdersTests(StoreApiFactory factory) : IClassFixture<StoreApiFactory>
{
    [Fact]
    public async Task DemoCustomer_SeesTheirEightOrders_NewestFirst()
    {
        using var client = await factory.LoginDemoCustomerAsync();

        var page = await client.GetOrdersAsync();

        page.TotalCount.ShouldBe(8);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(10);
        page.TotalPages.ShouldBe(1);
        page.Items.Select(order => order.OrderNumber).ShouldBe(
            ["ORD-0008", "ORD-0007", "ORD-0006", "ORD-0005", "ORD-0004", "ORD-0003", "ORD-0002", "ORD-0001"]);

        var first = page.Items[0];
        first.Status.ShouldBe("Pending");
        first.ItemCount.ShouldBe(1);
        first.Total.ShouldBe(750m);
        page.Items.Single(order => order.OrderNumber == "ORD-0001").ItemCount.ShouldBe(2);
    }

    [Fact]
    public async Task DeliveredOrderDetails_ShowTheSnapshots_AndCannotBeCancelled()
    {
        using var client = await factory.LoginDemoCustomerAsync();
        var id = await client.OrderIdAsync("ORD-0001");

        var response = await client.GetAsync($"/api/orders/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.ReadOrderAsync();
        order.OrderNumber.ShouldBe("ORD-0001");
        order.Status.ShouldBe("Delivered");
        order.City.ShouldBe("Cairo");
        order.Items.Select(item => (item.ProductName, item.ColorName, item.SizeName, item.UnitPrice, item.Quantity, item.LineTotal))
            .ShouldBe(
            [
                ("Classic Denim Jeans", "Blue", "M", 1100m, 1, 1100m),
                ("Classic Cotton T-Shirt", "White", "M", 350m, 2, 700m)
            ]);
        order.Subtotal.ShouldBe(1800m);
        order.ShippingFee.ShouldBe(50m);
        order.Total.ShouldBe(1850m);
        order.CanCancel.ShouldBeFalse();
    }

    [Fact]
    public async Task PendingOrderDetails_CanBeCancelled()
    {
        using var client = await factory.LoginDemoCustomerAsync();
        var id = await client.OrderIdAsync("ORD-0008");

        var order = await (await client.GetAsync($"/api/orders/{id}")).ReadOrderAsync();

        order.Status.ShouldBe("Pending");
        order.CanCancel.ShouldBeTrue();
        order.Items.ShouldHaveSingleItem().Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task NewCustomer_HasNoOrders()
    {
        var (client, _) = await factory.NewCustomerAsync();

        var page = await client.GetOrdersAsync();

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
        page.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task AnotherCustomersOrder_IsNotFound_ForDetailsAndCancel()
    {
        using var demo = await factory.LoginDemoCustomerAsync();
        var demoOrderId = await demo.OrderIdAsync("ORD-0008");
        var (other, _) = await factory.NewCustomerAsync();

        var details = await other.GetAsync($"/api/orders/{demoOrderId}");
        var cancel = await other.PostAsync($"/api/orders/{demoOrderId}/cancel", null);

        details.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await details.ReadErrorAsync()).Code.ShouldBe("orderNotFound");
        cancel.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await cancel.ReadErrorAsync()).Code.ShouldBe("orderNotFound");
        (await (await demo.GetAsync($"/api/orders/{demoOrderId}")).ReadOrderAsync()).Status.ShouldBe("Pending");
    }

    [Fact]
    public async Task UnknownOrder_IsNotFound()
    {
        using var client = await factory.LoginDemoCustomerAsync();

        var response = await client.GetAsync("/api/orders/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorAsync()).Code.ShouldBe("orderNotFound");
    }

    [Fact]
    public async Task Paging_SecondPageHasTheRest_PageBeyondTheLastIsEmpty()
    {
        using var client = await factory.LoginDemoCustomerAsync();

        var second = await client.GetOrdersAsync("?page=2&pageSize=5");
        var beyond = await client.GetOrdersAsync("?page=3&pageSize=5");

        second.Items.Select(order => order.OrderNumber).ShouldBe(["ORD-0003", "ORD-0002", "ORD-0001"]);
        second.TotalPages.ShouldBe(2);
        beyond.Items.ShouldBeEmpty();
        beyond.TotalCount.ShouldBe(8);
        beyond.TotalPages.ShouldBe(2);
    }

    [Theory]
    [InlineData("?pageSize=51", "pageSize")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?page=0", "page")]
    public async Task InvalidPaging_Returns400(string query, string field)
    {
        using var client = await factory.LoginDemoCustomerAsync();

        var response = await client.GetAsync($"/api/orders{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorAsync()).Errors.ShouldNotBeNull().ShouldContainKey(field);
    }
}
