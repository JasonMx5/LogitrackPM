using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models
{
    public class ShoppingCart
    {
        [Key]
        public int CartId { get; set; }

        [Required]
        public string UserId { get; set; }

        public DateTime ExpiresAt { get; set; }

        public List<ShoppingCartItem> Items { get; set; }

        public ShoppingCart()
        {
            Items = new List<ShoppingCartItem>();
        }
    }
}
