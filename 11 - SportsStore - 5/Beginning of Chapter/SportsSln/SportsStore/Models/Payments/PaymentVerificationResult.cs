namespace SportsStore.Models.Payments;

public class PaymentVerificationResult {
    public bool IsPaid { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? FailureReason { get; set; }
}

