using ECommerceMudblazorWebApp.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public ApplicationUser User { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        [Required]
        public OrderStatus Status { get; set; }

        [Required]
        [StringLength(500)]
        public string ShippingAddress { get; set; }

        [Required]
        [StringLength(100)]
        public string PaymentMethod { get; set; }

        // Navigation to order items
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        [NotMapped]
        public decimal TotalAmount => OrderItems?.Sum(item => item.UnitPrice * item.Quantity) ?? 0;
    }

    public enum OrderStatus
    {
        PENDING,
        PROCESSING,
        SHIPPED,
        DELIVERED,
        CANCELLED
    }
}
