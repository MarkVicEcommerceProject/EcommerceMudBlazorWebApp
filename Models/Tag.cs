using System.ComponentModel.DataAnnotations;

namespace ECommerceMudblazorWebApp.Models
{
    public class Tag
    {
        [Key] public int Id { get; set; }
        [Required][StringLength(100)] public string Name { get; set; }
        public List<ProductTag> ProductTags { get; set; } = [];
    }

}
