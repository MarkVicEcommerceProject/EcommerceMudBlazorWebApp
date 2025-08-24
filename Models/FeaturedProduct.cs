using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Models
{
    public class FeaturedProduct
    {
        [Key] public int Id { get; set; }
        public int ProductId { get; set; }
        public int Position { get; set; } = 0; 
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [ForeignKey(nameof(ProductId))] public Product Product { get; set; }
    }

}
