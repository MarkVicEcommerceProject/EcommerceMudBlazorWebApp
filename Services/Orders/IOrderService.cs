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
        Task<bool> UpdateOrderAsync(Order updatedOrder);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);

        Task<OrdersAnalytics> GetOrdersAnalyticsAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<TopCustomer>> GetTopCustomersAsync(int topCount = 5);
        Task<TimeSeriesDto> GetRevenueTimeSeriesAsync(DateTime startDate, DateTime endDate, string groupBy = "day");
        Task<FulfillmentMetricsDto> GetFulfillmentMetricsAsync(DateTime startDate, DateTime endDate, double targetHours = 24);
        Task<OrderStatusCountsDto> GetOrderStatusCountsAsync(DateTime? startDate = null, DateTime? endDate = null);

        Task<IEnumerable<TopProduct>> GetTopProductsAsync(DateTime startDate, DateTime endDate, int topCount);
    }
}