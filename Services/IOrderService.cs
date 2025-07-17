namespace ECommerceMudblazorWebApp.Services
{
    using ECommerceMudblazorWebApp.Data.Models;

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
    }
}