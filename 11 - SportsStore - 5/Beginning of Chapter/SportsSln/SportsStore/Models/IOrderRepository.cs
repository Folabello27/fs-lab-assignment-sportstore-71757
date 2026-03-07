namespace SportsStore.Models {

    public interface IOrderRepository {

        IQueryable<Order> Orders { get; }
        Order? GetOrder(int orderId);
        Order? GetOrderByStripeSessionId(string sessionId);
        void SaveOrder(Order order);
    }
}
