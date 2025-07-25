﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommerceMudblazorWebApp.Models
{
    public class ProductCategory
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int ProductId { get; set; }

        [Required]
        public int CategoryId { get; set; }
        [Required]
        [ForeignKey("ProductId")]
        public Product Product { get; set; }
        [Required]
        [ForeignKey("CategoryId")]
        public Category Category { get; set; }
    }
}