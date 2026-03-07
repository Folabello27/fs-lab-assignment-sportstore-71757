using Microsoft.EntityFrameworkCore;

namespace SportsStore.Models {

    public class EFOrderRepository : IOrderRepository {
        private StoreDbContext context;

        public EFOrderRepository(StoreDbContext ctx) {
            context = ctx;
        }

        public IQueryable<Order> Orders => context.Orders
                            .Include(o => o.Lines)
                            .ThenInclude(l => l.Product);

        public Order? GetOrder(int orderId) {
            return Orders.FirstOrDefault(o => o.OrderID == orderId);
        }

        public Order? GetOrderByStripeSessionId(string sessionId) {
            return Orders.FirstOrDefault(o => o.StripeCheckoutSessionId == sessionId);
        }

        public void SaveOrder(Order order) {
            context.AttachRange(order.Lines.Select(l => l.Product));
            if (order.OrderID == 0) {
                context.Orders.Add(order);
            }
            context.SaveChanges();
        }
    }
}
