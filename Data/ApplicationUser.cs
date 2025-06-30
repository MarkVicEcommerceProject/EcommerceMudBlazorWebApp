using ECommerceMudblazorWebApp.Data.Models;
using Microsoft.AspNetCore.Identity;

namespace ECommerceMudblazorWebApp.Data
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        /*public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string BillingAddress { get; set; } = string.Empty;*/

        // Navigation property for user's orders
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ShoppingCart? ShoppingCart { get; set; }
    }
}
