using ECommerceMudblazorWebApp.Models;

namespace ECommerceMudblazorWebApp.Components.Admin.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllProductsAsync();
        Task<Product> GetProductByIdAsync(int id);
        Task CreateProductAsync(Product product);
        Task UpdateProductAsync(Product product, IEnumerable<int> newCategoryIds);
        Task DeleteProductAsync(int id);
        Task<string> GenerateSKUAsync(string Category, string Name);
        //Task<bool> IsSKUUniqueAsync(string sku);

        Task<IEnumerable<InventoryAlert>> GetInventoryAlertsAsync(int threshold = 5);
    }
}