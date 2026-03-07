using Microsoft.AspNetCore.Mvc;
using Moq;
using SportsStore.Controllers;
using SportsStore.Models;
using SportsStore.Models.Payments;
using Xunit;

namespace SportsStore.Tests;

public class OrderControllerTests {

    [Fact]
    public void Cannot_Checkout_Empty_Cart() {
        // Arrange - create a mock repository
        Mock<IOrderRepository> mock = new();
        Mock<IPaymentService> paymentMock = new();
        // Arrange - create an empty cart
        Cart cart = new();
        // Arrange - create the order
        Order order = new();
        // Arrange - create an instance of the controller
        OrderController target = new(mock.Object, cart, paymentMock.Object);

        // Act
        ViewResult? result = target.Checkout(order) as ViewResult;

        // Assert - check that the order hasn't been stored
        mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
        // Assert - check that the method is returning the default view
        Assert.True(string.IsNullOrEmpty(result?.ViewName));
        // Assert - check that I am passing an invalid model to the view
        Assert.False(result?.ViewData.ModelState.IsValid);
    }

    [Fact]
    public void Cannot_Checkout_Invalid_ShippingDetails() {
        // Arrange - create a mock order repository
        Mock<IOrderRepository> mock = new();
        Mock<IPaymentService> paymentMock = new();
        // Arrange - create a cart with one item
        Cart cart = new();
        cart.AddItem(new Product(), 1);
        // Arrange - create an instance of the controller
        OrderController target = new(mock.Object, cart, paymentMock.Object);
        // Arrange - add an error to the model
        target.ModelState.AddModelError("error", "error");

        // Act - try to checkout
        ViewResult? result = target.Checkout(new Order()) as ViewResult;

        // Assert - check that the order hasn't been passed stored
        mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
        // Assert - check that the method is returning the default view
        Assert.True(string.IsNullOrEmpty(result?.ViewName));
        // Assert - check that I am passing an invalid model to the view
        Assert.False(result?.ViewData.ModelState.IsValid);
    }

    [Fact]
    public void Can_Checkout_And_Redirect_To_Stripe() {
        // Arrange - create a mock order repository
        Mock<IOrderRepository> mock = new();
        Mock<IPaymentService> paymentMock = new();
        // Arrange - create a cart with one item
        Cart cart = new();
        cart.AddItem(new Product { Name = "Ball", Price = 10m }, 1);
        paymentMock.Setup(p => p.CreateCheckoutSession(
                It.IsAny<Order>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(new PaymentSessionResult {
                SessionId = "cs_test_123",
                CheckoutUrl = "https://checkout.stripe.com/c/pay/cs_test_123"
            });

        // Arrange - create an instance of the controller
        OrderController target = new(mock.Object, cart, paymentMock.Object);

        // Act - try to checkout
        RedirectResult? result = target.Checkout(new Order()) as RedirectResult;

        // Assert - pending order saved and updated with session ID
        mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Exactly(2));
        paymentMock.Verify(p => p.CreateCheckoutSession(
                It.IsAny<Order>(),
                It.IsAny<string>(),
                It.IsAny<string>()),
            Times.Once);
        Assert.Equal("https://checkout.stripe.com/c/pay/cs_test_123", result?.Url);
    }
}

