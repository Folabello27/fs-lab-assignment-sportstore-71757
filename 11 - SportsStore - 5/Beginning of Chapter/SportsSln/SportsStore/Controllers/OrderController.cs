using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Stripe;
using SportsStore.Models;
using SportsStore.Models.Payments;

namespace SportsStore.Controllers;

public class OrderController : Controller {
    private readonly IOrderRepository repository;
    private readonly Cart cart;
    private readonly ILogger<OrderController> logger;
    private readonly IPaymentService paymentService;

    public OrderController(
        IOrderRepository repoService,
        Cart cartService,
        IPaymentService? paymentService = null,
        ILogger<OrderController>? logger = null) {
        repository = repoService;
        cart = cartService;
        this.paymentService = paymentService ?? new NoOpPaymentService();
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

        if (!cart.Lines.Any()) {
            ModelState.AddModelError("", "Sorry, your cart is empty!");
            logger.LogWarning("Checkout rejected because cart is empty");
        }

        if (!ModelState.IsValid) {
            logger.LogWarning(
                "Checkout validation failed. ValidationErrorCount={ValidationErrorCount}",
                ModelState.ErrorCount);
            return View();
        }

        try {
            order.Lines = cart.Lines.ToArray();
            order.PaymentStatus = "Pending";
            repository.SaveOrder(order);

            var (successUrl, cancelUrl) = BuildCheckoutUrls(order.OrderID);
            PaymentSessionResult sessionResult =
                paymentService.CreateCheckoutSession(order, successUrl, cancelUrl);

            order.StripeCheckoutSessionId = sessionResult.SessionId;
            order.PaymentFailureReason = null;
            repository.SaveOrder(order);

            logger.LogInformation(
                "Stripe checkout created. OrderId={OrderId}, StripeSessionId={StripeSessionId}",
                order.OrderID,
                sessionResult.SessionId);

            return Redirect(sessionResult.CheckoutUrl);
        } catch (InvalidOperationException ex) {
            logger.LogError(ex, "Checkout configuration error for OrderId={OrderId}", order.OrderID);
            order.PaymentStatus = "Failed";
            order.PaymentFailureReason = "Payment configuration is missing or invalid.";
            repository.SaveOrder(order);
            ModelState.AddModelError("", "Payment is unavailable right now. Please try again later.");
            return View(order);
        } catch (StripeException ex) {
            logger.LogError(ex, "Stripe error while creating checkout for OrderId={OrderId}", order.OrderID);
            order.PaymentStatus = "Failed";
            order.PaymentFailureReason = ex.Message;
            repository.SaveOrder(order);
            ModelState.AddModelError("", "Payment provider error. Please try again.");
            return View(order);
        } catch (Exception ex) {
            logger.LogError(ex, "Unexpected checkout error for OrderId={OrderId}", order.OrderID);
            order.PaymentStatus = "Failed";
            order.PaymentFailureReason = "Unexpected error while preparing payment.";
            repository.SaveOrder(order);
            ModelState.AddModelError("", "Unexpected error while preparing payment.");
            return View(order);
        }
    }

    [HttpGet]
    public IActionResult PaymentSuccess(string sessionId) {
        if (string.IsNullOrWhiteSpace(sessionId)) {
            logger.LogWarning("PaymentSuccess called without sessionId");
            return RedirectToAction(nameof(PaymentFailed), new { message = "Missing payment session." });
        }

        Order? order = repository.GetOrderByStripeSessionId(sessionId);
        if (order == null) {
            logger.LogWarning("PaymentSuccess received unknown Stripe session {StripeSessionId}", sessionId);
            return RedirectToAction(nameof(PaymentFailed), new { message = "Payment session not found." });
        }

        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)) {
            logger.LogInformation(
                "PaymentSuccess replay for already paid order. OrderId={OrderId}",
                order.OrderID);
            return RedirectToPage("/Completed", new { orderId = order.OrderID });
        }

        try {
            PaymentVerificationResult verification = paymentService.VerifyCheckoutSession(sessionId);
            if (!verification.IsPaid) {
                order.PaymentStatus = "Failed";
                order.PaymentFailureReason = verification.FailureReason ?? "Payment was not completed.";
                repository.SaveOrder(order);

                logger.LogWarning(
                    "Payment verification failed. OrderId={OrderId}, StripeSessionId={StripeSessionId}, Reason={Reason}",
                    order.OrderID,
                    sessionId,
                    order.PaymentFailureReason);

                return RedirectToAction(nameof(PaymentFailed), new { orderId = order.OrderID });
            }

            order.PaymentStatus = "Paid";
            order.StripePaymentIntentId = verification.PaymentIntentId;
            order.PaymentConfirmedAtUtc = DateTime.UtcNow;
            order.PaymentFailureReason = null;
            repository.SaveOrder(order);
            cart.Clear();

            logger.LogInformation(
                "Payment completed successfully. OrderId={OrderId}, StripeSessionId={StripeSessionId}, PaymentIntentId={PaymentIntentId}",
                order.OrderID,
                sessionId,
                verification.PaymentIntentId);

            return RedirectToPage("/Completed", new { orderId = order.OrderID });
        } catch (StripeException ex) {
            logger.LogError(ex,
                "Stripe verification error. OrderId={OrderId}, StripeSessionId={StripeSessionId}",
                order.OrderID,
                sessionId);
            order.PaymentStatus = "Failed";
            order.PaymentFailureReason = ex.Message;
            repository.SaveOrder(order);
            return RedirectToAction(nameof(PaymentFailed), new { orderId = order.OrderID });
        } catch (Exception ex) {
            logger.LogError(ex,
                "Unexpected verification error. OrderId={OrderId}, StripeSessionId={StripeSessionId}",
                order.OrderID,
                sessionId);
            order.PaymentStatus = "Failed";
            order.PaymentFailureReason = "Unexpected verification error.";
            repository.SaveOrder(order);
            return RedirectToAction(nameof(PaymentFailed), new { orderId = order.OrderID });
        }
    }

    [HttpGet]
    public IActionResult PaymentCancelled(int orderId) {
        Order? order = repository.GetOrder(orderId);
        if (order != null && !string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)) {
            order.PaymentStatus = "Cancelled";
            order.PaymentFailureReason = "Customer cancelled Stripe checkout.";
            repository.SaveOrder(order);
        }

        logger.LogInformation("Payment cancelled by customer. OrderId={OrderId}", orderId);
        return View(model: orderId);
    }

    [HttpGet]
    public IActionResult PaymentFailed(int? orderId, string? message) {
        if (orderId.HasValue) {
            logger.LogWarning("Payment failed for OrderId={OrderId}", orderId.Value);
        } else {
            logger.LogWarning("Payment failed without OrderId. Message={Message}", message);
        }

        ViewData["Message"] = message;
        return View(model: orderId);
    }

    private (string SuccessUrl, string CancelUrl) BuildCheckoutUrls(int orderId) {
        var request = HttpContext?.Request;
        string baseUrl = (request?.Scheme, request?.Host.HasValue) switch {
            ("http" or "https", true) => $"{request.Scheme}://{request.Host}",
            _ => "https://localhost"
        };

        return (
            $"{baseUrl}/Order/PaymentSuccess?sessionId={{CHECKOUT_SESSION_ID}}",
            $"{baseUrl}/Order/PaymentCancelled?orderId={orderId}"
        );
    }

    private sealed class NoOpPaymentService : IPaymentService {
        public PaymentSessionResult CreateCheckoutSession(Order order, string successUrl, string cancelUrl) {
            throw new InvalidOperationException(
                "Payment service is unavailable. Register IPaymentService in dependency injection.");
        }

        public PaymentVerificationResult VerifyCheckoutSession(string sessionId) {
            throw new InvalidOperationException(
                "Payment service is unavailable. Register IPaymentService in dependency injection.");
        }
    }
}
