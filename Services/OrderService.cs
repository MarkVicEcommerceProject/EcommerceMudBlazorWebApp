namespace ECommerceMudblazorWebApp.Services
{
    using ECommerceMudblazorWebApp.Data;
    using ECommerceMudblazorWebApp.Data.Models;
    using Microsoft.EntityFrameworkCore;

    public class OrderService : IOrderService
    {

        public event Action? OnChange;

        public Order? CurrentOrder { get; set; }
        private readonly List<Order> _orders = new();
        private int _nextId = 1;
        public async Task<int> PlaceOrderAsync(Order order)
        {
            // Implementation for placing an order
            order.Id = _nextId++;
            _orders.Add(order);
            OnChange?.Invoke();
            return await Task.FromResult(order.Id);
        }

        public async Task<Order> GetOrderByIdAsync(int orderId)
        {
            // Implementation for retrieving an order by ID
            return await Task.FromResult(new Order { Id = orderId });
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId)
        {
            // Implementation for retrieving orders by user ID
            return await Task.FromResult(new List<Order>());
        }

        public async Task CancelOrderAsync(int orderId)
        {
            // Implementation for canceling an order
            OnChange?.Invoke();
            await Task.CompletedTask;
        }

        public async Task<Order?> GetOrderDetailsAsync(int orderId)
        {
            // Implementation for getting order details
            return await Task.FromResult(new Order { Id = orderId });
        }

        public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        {
            // Implementation for getting all orders
            return await Task.FromResult(new List<Order>());
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            // Implementation for updating the status of an order
            OnChange?.Invoke();
            return await Task.FromResult(true);
        }
    }

   /* public class EfOrderService : IOrderService
    {
        public event Action? OnChange;
        public Order? CurrentOrder { get; set; }
        private readonly ApplicationDbContext _db;
        public EfOrderService(ApplicationDbContext dbContext)
        {
            _db = dbContext;
        }
        public async Task<int> PlaceOrderAsync(Order order)
        {
            //starting Transaction
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                order.OrderDate = DateTime.UtcNow;
                order.Status = OrderStatus.PENDING;

                foreach (var item in order.OrderItems)
                {
                    _db.Entry(item).State = EntityState.Added;
                }

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return order.Id;

            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
            
        }
    }*/

}