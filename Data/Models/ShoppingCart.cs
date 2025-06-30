namespace ECommerceMudblazorWebApp.Data.Models
{
    public class ShoppingCart
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string GuestId { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public ICollection<CartItem> Items = new List<CartItem>(); 

    }
}
