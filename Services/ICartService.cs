namespace ECommerceMudblazorWebApp.Services
{
    using ECommerceMudblazorWebApp.Models;

    public interface ICartService
    {
        event Action? OnChange;
        ShoppingCart? ShoppingCart { get; set; }
        void AddToCart(CartItem item);
        void RemoveFromCart(int productId);
        void UpdateQuantity(int productId, int quantity);
        int GetCartItemCount();
        void ClearCart();
    }
}