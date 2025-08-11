namespace ECommerceMudblazorWebApp.Services.Orders
{
    using ECommerceMudblazorWebApp.Models;

    public interface IOrderService
    {
        event Action? OnChange;
        Order? CurrentOrder { get; set; }
        Task<int> PlaceOrderAsync(Order order);
        Task<Order> GetOrderByIdAsync(int orderId);
        Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId);
        Task CancelOrderAsync(int orderId);
        Task<Order?> GetOrderDetailsAsync(int orderId);
        Task<IEnumerable<Order>> GetAllOrdersAsync();
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);

        Task<OrdersAnalytics> GetOrdersAnalyticsAsync(DateTime startDate, DateTime endDate);
    }

    public class OrdersAnalytics
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int FulfilledOrders { get; set; }
        public int PendingOrders { get; set; }
    }
}