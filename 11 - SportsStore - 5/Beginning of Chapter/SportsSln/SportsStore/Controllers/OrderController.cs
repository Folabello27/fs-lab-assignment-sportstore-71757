using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SportsStore.Models;

namespace SportsStore.Controllers {

    public class OrderController : Controller {
        private readonly IOrderRepository repository;
        private readonly Cart cart;
        private readonly ILogger<OrderController> logger;

        public OrderController(
            IOrderRepository repoService,
            Cart cartService,
            ILogger<OrderController>? logger = null) {
            repository = repoService;
            cart = cartService;
            this.logger = logger ?? NullLogger<OrderController>.Instance;
        }

        public ViewResult Checkout() {
            logger.LogInformation(
                "Checkout page requested. CurrentCartItemCount={CartItemCount}",
                cart.Lines.Sum(l => l.Quantity));
            return View(new Order());
        }

        [HttpPost]
        public IActionResult Checkout(Order order) {
            logger.LogInformation(
                "Checkout submitted. CartLineCount={CartLineCount}, CartItemCount={CartItemCount}",
                cart.Lines.Count(),
                cart.Lines.Sum(l => l.Quantity));

            if (cart.Lines.Count() == 0) {
                ModelState.AddModelError("", "Sorry, your cart is empty!");
                logger.LogWarning("Checkout rejected because cart is empty");
            }

            if (ModelState.IsValid) {
                order.Lines = cart.Lines.ToArray();
                repository.SaveOrder(order);

                var orderItemCount = order.Lines.Sum(l => l.Quantity);
                var orderTotal = order.Lines.Sum(l => l.Product.Price * l.Quantity);
                logger.LogInformation(
                    "Order created successfully. OrderId={OrderId}, ItemCount={OrderItemCount}, OrderTotal={OrderTotal}",
                    order.OrderID,
                    orderItemCount,
                    orderTotal);

                cart.Clear();
                return RedirectToPage("/Completed", new { orderId = order.OrderID });
            } else {
                logger.LogWarning(
                    "Checkout validation failed. ValidationErrorCount={ValidationErrorCount}",
                    ModelState.ErrorCount);
                return View();
            }
        }
    }
}
