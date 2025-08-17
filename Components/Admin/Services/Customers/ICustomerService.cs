using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Components.Admin.Services.Customers
{
    public interface ICustomerService
    {
        Task<IEnumerable<ApplicationUser>> GetAllCustomersAsync();
        Task<ApplicationUser?> GetCustomerByIdAsync(string userId);
        Task<CustomerAnalytics> GetCustomerAnalyticsAsync(DateTime startDate, DateTime endDate);
        Task<CustomerTypeStats> GetCustomerTypeStatsAsync(DateTime? startDate = null, DateTime? endDate = null);
    }

    public class CustomerTypeStats
    {
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public double[] ToArray() => [NewCustomers, ReturningCustomers];
    }
}
