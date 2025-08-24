using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Models
{
    public class ProductTag
    {
        [Key] public int Id { get; set; }
        [Required] public int ProductId { get; set; }
        [Required] public int TagId { get; set; }

        [ForeignKey(nameof(ProductId))] public Product Product { get; set; }
        [ForeignKey(nameof(TagId))] public Tag Tag { get; set; }
    }
}
