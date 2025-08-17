using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommerceMudblazorWebApp.Components.Admin.Services.Customers
{
    public class CustomerService(IDbContextFactory<ApplicationDbContext> contextFactory) : ICustomerService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory = contextFactory;

        public async Task<IEnumerable<ApplicationUser>> GetAllCustomersAsync()
        {
            using var _context = _contextFactory.CreateDbContext();
            return await _context.Users.ToListAsync();
        }

        public async Task<ApplicationUser?> GetCustomerByIdAsync(string userId)
        {
            using var _context = _contextFactory.CreateDbContext();
            return await _context.Users
                .Include(u => u.Orders)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<CustomerAnalytics> GetCustomerAnalyticsAsync(DateTime startDate, DateTime endDate)
        {
            using var _context = _contextFactory.CreateDbContext();
            var totalNow = await _context.Users.CountAsync();
            Console.WriteLine(totalNow);
            var totalBeforePeriod = await _context.Users
                .Where(u => u.Orders.Any(o => o.OrderDate < startDate))
                .CountAsync();

            Console.WriteLine(totalBeforePeriod);

            double change = totalBeforePeriod > 0
                ? (double)(totalNow - totalBeforePeriod) / totalBeforePeriod * 100
                : 0;

            string sign = totalBeforePeriod > 0 ? "+" : " ";
            string direction = change < 0 ? "decrease" : (change > 0 ? "increase" : "no change");

            return new CustomerAnalytics
            {
                TotalCustomers = totalNow,
                CustomerChange = $"{sign} {Math.Round(change, 2)} {direction}"
            };
        }

        public async Task<CustomerTypeStats> GetCustomerTypeStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            using var _context = _contextFactory.CreateDbContext();
            var query = _context.Orders
                        .Include(o => o.User)
                        .AsQueryable();

            if (startDate.HasValue)
                query = query.Where(o => o.OrderDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(o => o.OrderDate <= endDate.Value);

            var allOrders = await query.ToListAsync();

            var customerOrderGroups = allOrders
                .GroupBy(o => o.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    OrderCount = g.Count()
                })
                .ToList();
                
            return new CustomerTypeStats
            {
                NewCustomers = customerOrderGroups.Count(c => c.OrderCount == 1),
                ReturningCustomers = customerOrderGroups.Count(c => c.OrderCount > 1)
            };
        }
    }
}
