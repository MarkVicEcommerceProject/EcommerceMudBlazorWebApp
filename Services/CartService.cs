using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Services
{
    public class CartService : ICartService
    {
        public event Action? OnChange;
        public ShoppingCart? ShoppingCart { get; set; } = new();

        public void AddToCart(CartItem item)
        {
            var existingItem = ShoppingCart?.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existingItem is not null)
            {
                existingItem.Quantity += item.Quantity;
                Console.WriteLine($"{existingItem.Quantity}");
            }
            else
            {
                ShoppingCart?.Items.Add(item);
                Console.WriteLine($"Added {ShoppingCart?.Items.FirstOrDefault(i => i.ProductId == item.ProductId)?.Quantity ?? 1} of product {item.ProductId} to cart.");
            }
            OnChange?.Invoke();
        }

        public void RemoveFromCart(int productId)
        {
            var item = ShoppingCart?.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                ShoppingCart.Items.Remove(item);
            }
        }

        public void UpdateQuantity(int productId, int quantity)
        {
            var item = ShoppingCart?.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item is null)
                return;

            if (quantity <= 0)
                ShoppingCart?.Items.Remove(item);
            else
                item.Quantity = quantity;

            OnChange?.Invoke();
        }

        public int GetCartItemCount() => ShoppingCart?.Items.Sum(i => i.Quantity) ?? 0;
        public void ClearCart()
        {
            if (ShoppingCart != null)
            {
                ShoppingCart.Items.Clear();
                OnChange?.Invoke();
                Console.WriteLine("Cart cleared.");
            }
        }
    }
}