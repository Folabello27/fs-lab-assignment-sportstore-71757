namespace SportsStore.Models.Payments;

public class PaymentSessionResult {
    public string SessionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
}

