using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Components.Admin.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync();
        Task<Product> GetProductByIdAsync(int id);
        Task CreateProductAsync(Product product);
        Task<bool> UpdateProductAsync(Product product,IEnumerable<int> newCategoryIds,IEnumerable<int>? newTagIds = null,bool setFeatured = false,DateTime? featuredStart = null,DateTime? featuredEnd = null,
    bool setDailyDeal = false,decimal? dailyDealPrice = null,DateTime? dailyDealStart = null,DateTime? dailyDealEnd = null,bool setFlashSale = false,decimal? flashSalePrice = null,
    DateTime? flashSaleStart = null,DateTime? flashSaleEnd = null);
        Task DeleteProductAsync(int id);
        Task<string> GenerateSKUAsync(string Category, string Name);
        //Task<bool> IsSKUUniqueAsync(string sku);

        //Tags
        Task<List<Tag>> GetProductTagsAsync(int productId);
        Task AddTagToProductAsync(int productId, int tagId);
        Task RemoveTagFromProductAsync(int productId, int tagId);

        Task<IEnumerable<InventoryAlert>> GetInventoryAlertsAsync(int threshold = 5);
        Task RecordProductViewAsync(int productId, string? userId = null, string? guestId = null);
        


    }
}