using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Services.Cart
{
    public interface ICartService
    {
        event Action? OnChange;

        Task AddToCartAsync(int productId, int quantity, string? userId, string guestId);
        Task RemoveFromCartAsync(int productId, string? userId, string guestId);
        Task UpdateQuantityAsync(int productId, int quantity, string? userId, string guestId);
        Task<int> GetCartItemCountAsync(string? userId, string guestId);
        Task ClearCartAsync(string? userId, string guestId);
        Task<List<CartItem>> GetCartItemsAsync(string? userId, string guestId);
        Task<bool> MergeGuestCartToUserAsync(string guestId, string userId);
    }
}
