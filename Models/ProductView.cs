using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ECommerceMudblazorWebApp.Data;

namespace ECommerceMudblazorWebApp.Models
{
    public class ProductView
    {
        [Key] public int Id { get; set; }
        public int ProductId { get; set; }
        public string? UserId { get; set; }  
        public string? GuestId { get; set; }
        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ProductId))] public Product Product { get; set; }
        [ForeignKey(nameof(UserId))] public ApplicationUser User { get; set; }
    }

}
