using ECommerceMudblazorWebApp.Data;
using ECommerceMudblazorWebApp.Models;
using ECommerceMudblazorWebApp.Services.Cart;
using Microsoft.EntityFrameworkCore;
using System.Threading.Channels;


namespace ECommerceMudblazorWebApp.Services.Cart
{
    public class CartService(IDbContextFactory<ApplicationDbContext> contextFactory) : ICartService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory = contextFactory;
        public event Action? OnChange;

        private async Task<ShoppingCart?> GetOrCreateCartAsync(ApplicationDbContext _context, string? userId, string? guestId)
        {
            
            ShoppingCart? cart = null;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                //Check if user exists before proceeding
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    // Optionally log this
                    Console.WriteLine($"Warning: Tried to create cart for non-existent user {userId}");
                    return null;
                }

                cart = await _context.ShoppingCarts
                    .Include(c => c.Items).ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.GuestId == null);

                if (cart == null)
                {
                    cart = new ShoppingCart
                    {
                        UserId = userId,
                        GuestId = null,
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.ShoppingCarts.Add(cart);
                    await _context.SaveChangesAsync();
                }
            }
            else if (!string.IsNullOrWhiteSpace(guestId))
            {
                cart = await _context.ShoppingCarts
                    .Include(c => c.Items).ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.GuestId == guestId && c.UserId == null);

                if (cart == null)
                {
                    cart = new ShoppingCart
                    {
                        GuestId = guestId,
                        CreatedDate = DateTime.UtcNow
                    };
                    _context.ShoppingCarts.Add(cart);
                    await _context.SaveChangesAsync();
                }
            }

            return cart;
        }



        public async Task AddToCartAsync(int productId, int quantity, string? userId, string? guestId)
        {
            await using var _context = _contextFactory.CreateDbContext();
            var cart = await GetOrCreateCartAsync(_context,userId, guestId);
            Console.WriteLine($"[DEBUG] Using ShoppingCart.Id={cart.Id} for user={userId}, guest={guestId}");
            var existing = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                var product = await _context.Products.FindAsync(productId)
                    ?? throw new KeyNotFoundException($"Product {productId} not found.");
                cart.Items.Add(new CartItem
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.Price,
                    ShoppingCartId = cart.Id
                });
            }
            await _context.SaveChangesAsync();
            Console.WriteLine($"[DEBUG] SaveChanges wrote entries");
            OnChange?.Invoke();
        }

        public async Task RemoveFromCartAsync(int productId, string? userId, string guestId)
        {
            await using var _context = _contextFactory.CreateDbContext();
            var cart = await GetOrCreateCartAsync(_context,userId, guestId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                cart.Items.Remove(item);
                await _context.SaveChangesAsync();
                OnChange?.Invoke();
            }
        }

        public async Task UpdateQuantityAsync(int productId, int quantity, string? userId, string guestId)
        {
            await using var _context = _contextFactory.CreateDbContext();
            var cart = await GetOrCreateCartAsync(_context,userId, guestId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) return;

            if (quantity <= 0)
                cart.Items.Remove(item);
            else
                item.Quantity = quantity;

            await _context.SaveChangesAsync();
            OnChange?.Invoke();
        }

        public async Task<int> GetCartItemCountAsync(string? userId, string guestId)
        {
            await using var _context = _contextFactory.CreateDbContext();
            ApplicationUser? user = null;
            if (!string.IsNullOrEmpty(userId))
            {
                user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    Console.WriteLine($"Tried to create cart for non-existent user {userId}");
                    userId = null;
                }
            }

            string? activeUserId = userId ?? guestId;
            if (string.IsNullOrEmpty(activeUserId))
            {
                Console.WriteLine("No valid userId or guestId found to get cart item count.");
                return 0;
            }
            var cart = await GetOrCreateCartAsync(_context,userId, guestId);
            return cart.Items.Sum(i => i.Quantity);
        }

        public async Task<List<CartItem>> GetCartItemsAsync(string? userId, string guestId)
        {
            await using var _context = _contextFactory.CreateDbContext();
            ApplicationUser? user = null;
            if (!string.IsNullOrEmpty(userId))
            {
                user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    Console.WriteLine($"Tried to Get or Create cart for non-existent user {userId}");
                    userId = null;
                }
            }
            if (string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(guestId))
            {
                // Edge case: nothing to use
                return new List<CartItem>();
            }
            var cart = await GetOrCreateCartAsync(_context,userId, guestId);
            return cart.Items?.ToList() ?? new List<CartItem>();
        }

        public async Task ClearCartAsync(string? userId, string guestId)
        {
            await using var _context = _contextFactory.CreateDbContext();
            var cart = await GetOrCreateCartAsync(_context,userId, guestId);
            if (cart == null) return;
            cart.Items.Clear();
            await _context.SaveChangesAsync();
            OnChange?.Invoke();
        }

        public async Task<bool> MergeGuestCartToUserAsync(string guestId, string userId)
        {
            await using var _context = _contextFactory.CreateDbContext();
            try
            {
                Console.WriteLine($"Merging guest cart {guestId} into user {userId}");
                // Load guest cart
                var guestCart = await _context.ShoppingCarts
                    .Include(c => c.Items).ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.GuestId == guestId);
                if (guestCart == null) return false;

                // Load or create user cart
                var userCart = await _context.ShoppingCarts
                    .Include(c => c.Items).ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.UserId == userId)
                    ?? new ShoppingCart { UserId = userId, CreatedDate = DateTime.UtcNow };

                if (userCart.Id == 0) _context.ShoppingCarts.Add(userCart);

                // Merge items
                foreach (var item in guestCart.Items)
                {
                    var existing = userCart.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
                    if (existing != null)
                        existing.Quantity += item.Quantity;
                    else
                        userCart.Items.Add(new CartItem
                        {
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice
                        });
                }

                // Remove guest cart
                if (guestCart.Id != 0)
                    _context.ShoppingCarts.Remove(guestCart);

                await _context.SaveChangesAsync();
                OnChange?.Invoke();
                return true;
            }
            catch (Exception)
            {
                Console.WriteLine($"Error merging cart: {guestId} into user: {userId}");
                return false;

            }
        }
    }
}
