using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace SportsStore.Models.Payments;

public class StripePaymentService : IPaymentService {
    private readonly StripeOptions options;

    public StripePaymentService(IOptions<StripeOptions> optionsAccessor) {
        options = optionsAccessor.Value;
    }

    public PaymentSessionResult CreateCheckoutSession(Order order, string successUrl, string cancelUrl) {
        try {
            EnsureConfigured();

            StripeConfiguration.ApiKey = options.SecretKey;
            var service = new SessionService();

            var sessionOptions = new SessionCreateOptions {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Metadata = new Dictionary<string, string> {
                    ["OrderId"] = order.OrderID.ToString()
                },
                LineItems = order.Lines.Select(l => new SessionLineItemOptions {
                    Quantity = l.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions {
                        Currency = "usd",
                        UnitAmount = Decimal.ToInt64(Decimal.Round(l.Product.Price * 100m)),
                        ProductData = new SessionLineItemPriceDataProductDataOptions {
                            Name = l.Product.Name
                        }
                    }
                }).ToList()
            };

            Session session = service.Create(sessionOptions);

            if (string.IsNullOrWhiteSpace(session.Id) || string.IsNullOrWhiteSpace(session.Url)) {
                throw new InvalidOperationException("Stripe did not return a valid checkout session.");
            }

            return new PaymentSessionResult {
                SessionId = session.Id,
                CheckoutUrl = session.Url
            };
        } catch (StripeException) {
            throw;
        } catch (Exception ex) {
            throw new InvalidOperationException("Failed to create checkout session.", ex);
        }
    }

    public PaymentVerificationResult VerifyCheckoutSession(string sessionId) {
        try {
            EnsureConfigured();

            StripeConfiguration.ApiKey = options.SecretKey;
            var service = new SessionService();
            Session session = service.Get(sessionId);

            var isPaid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
            return new PaymentVerificationResult {
                IsPaid = isPaid,
                PaymentIntentId = session.PaymentIntentId,
                FailureReason = isPaid
                    ? null
                    : $"Stripe payment status was '{session.PaymentStatus}'."
            };
        } catch (StripeException) {
            throw;
        } catch (Exception ex) {
            throw new InvalidOperationException("Failed to verify checkout session.", ex);
        }
    }

    private void EnsureConfigured() {
        if (string.IsNullOrWhiteSpace(options.SecretKey)) {
            throw new InvalidOperationException(
                "Stripe SecretKey is missing. Configure Stripe:SecretKey using user-secrets or environment variables.");
        }
    }
}

