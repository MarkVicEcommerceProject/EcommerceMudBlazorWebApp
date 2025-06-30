namespace ECommerceMudblazorWebApp.Data.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public String Slug { get; set; }
        public int? ParentCategoryId { get; set; }
        public Category ParentCategory { get; set; }

        public ICollection<Category> ChildCategories { get; set; } = new List<Category>();

        public List<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
        public Category() { }
    }
}
