namespace ECommerceMudblazorWebApp.Services.Orders
{
    using ECommerceMudblazorWebApp.Data;
    using ECommerceMudblazorWebApp.Models;
    using Microsoft.EntityFrameworkCore;

    public class EfOrderService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IOrderService
    {
        public event Action? OnChange;
        public Order? CurrentOrder { get; set; }
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory = dbContextFactory;

        public async Task<int> PlaceOrderAsync(Order order)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
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
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .SingleOrDefaultAsync(o => o.Id == orderId)
                ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        public async Task<IEnumerable<Order>> GetOrdersByUserIdAsync(string userId)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ToListAsync()
                ?? throw new KeyNotFoundException($"No orders found for user ID {userId}.");
        }

        public async Task CancelOrderAsync(int orderId)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
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
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .SingleOrDefaultAsync(o => o.Id == orderId)
                ?? throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        {
            await using var _db = _dbContextFactory.CreateDbContext();
            return await _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ToListAsync()
                ?? throw new InvalidOperationException("No orders found.");
        }
        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            await using var _db = _dbContextFactory.CreateDbContext();
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

        public async Task<OrdersAnalytics> GetOrdersAnalyticsAsync(DateTime startDate, DateTime endDate)
        {
            await using var _db = _dbContextFactory.CreateDbContext();

            var orders = await _db.Orders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .ToListAsync();

            int totalOrders = orders.Count;
            decimal totalRevenue = orders.Sum(o => o.TotalAmount);
            int fulfilledOrders = orders.Count(o =>
                o.Status == OrderStatus.DELIVERED || o.Status == OrderStatus.SHIPPED);
            int pendingOrders = orders.Count(o =>
                o.Status == OrderStatus.PENDING || o.Status == OrderStatus.PROCESSING);

            return new OrdersAnalytics
            {
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                FulfilledOrders = orders.Count(o =>
                    o.Status == OrderStatus.DELIVERED || o.Status == OrderStatus.SHIPPED),
                PendingOrders = orders.Count(o =>
                    o.Status == OrderStatus.PENDING || o.Status == OrderStatus.PROCESSING)
            };

        }
    }

}
