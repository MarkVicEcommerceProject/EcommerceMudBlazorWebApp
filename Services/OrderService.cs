namespace ECommerceMudblazorWebApp.Services
{
    using ECommerceMudblazorWebApp.Data;
    using ECommerceMudblazorWebApp.Models;
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
            var order = _orders.FirstOrDefault(o => o.Id == orderId);
            return await Task.FromResult(order);
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId)
        {
            // Implementation for retrieving orders by user ID
            var userOrders = _orders.Where(o => o.UserId == userId).AsEnumerable();
            return await Task.FromResult(userOrders);
        }

        public async Task CancelOrderAsync(int orderId)
        {
            // Implementation for canceling an order
            _orders.RemoveAll(o => o.Id == orderId);
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
            return await Task.FromResult(_orders.AsEnumerable());
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            // Implementation for updating the status of an order
            var order = _orders.SingleOrDefault(o => o.Id == orderId);
            if (order == null) return await Task.FromResult(false);
            order.Status = newStatus;
            return await Task.FromResult(true);
        }
    }

    public class EfOrderService : IOrderService
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

        public async Task<Order> GetOrderByIdAsync(int orderId)
        {
            return await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .SingleOrDefaultAsync(o => o.Id == orderId)
                ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId)
        {
            return await _db.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ToListAsync()
                ?? throw new KeyNotFoundException($"No orders found for user ID {userId}.");
        }

        public async Task CancelOrderAsync(int orderId)
        {
            var order = await _db.Orders.FindAsync(orderId);
            if (order == null) throw new KeyNotFoundException($"Order with ID {orderId} not found.");

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                order.Status = OrderStatus.CANCELLED;
                _db.Orders.Update(order);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                OnChange?.Invoke();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<Order?> GetOrderDetailsAsync(int orderId)
        {
            return await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .SingleOrDefaultAsync(o => o.Id == orderId)
                ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        {
            return await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ToListAsync()
                ?? throw new InvalidOperationException("No orders found.");
        }
        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            var order = await _db.Orders.FindAsync(orderId);
            if (order == null) throw new KeyNotFoundException($"Order with ID {orderId} not found.");
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                order.Status = newStatus;
                _db.Orders.Update(order);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                OnChange?.Invoke();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

    }

    public static class OrderServiceExtensions
    {
        public static IServiceCollection AddOrderServices(this IServiceCollection services, bool useMock = false)
        {
            if (useMock)
            {
                services.AddScoped<IOrderService, OrderService>();
            }
            else
            {
                services.AddScoped<IOrderService, EfOrderService>();
            }
            return services;
        }
    }
}