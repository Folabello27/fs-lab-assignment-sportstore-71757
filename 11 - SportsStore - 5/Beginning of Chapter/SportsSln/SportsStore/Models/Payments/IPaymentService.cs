namespace SportsStore.Models.Payments;

public interface IPaymentService {
    PaymentSessionResult CreateCheckoutSession(Order order, string successUrl, string cancelUrl);
    PaymentVerificationResult VerifyCheckoutSession(string sessionId);
}

