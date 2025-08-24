using System.ComponentModel.DataAnnotations;

namespace ECommerceMudblazorWebApp.Models
{
    public class WishlistItem
    {
        [Key] public int Id { get; set; }
        public string UserId { get; set; }
        public int ProductId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

}
