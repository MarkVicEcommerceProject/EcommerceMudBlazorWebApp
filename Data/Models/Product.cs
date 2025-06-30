namespace ECommerceMudblazorWebApp.Data.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }

        public String SKU { get; set; }
        public int StockQuantity { get; set; }
        public string ImagePath { get; set; }

        public string ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        //public List<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();


    }
}
