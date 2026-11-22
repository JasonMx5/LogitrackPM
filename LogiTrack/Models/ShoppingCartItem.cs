using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models
{
    public class ShoppingCartItem
    {
        [Key]
        public int CartItemId { get; set; }

        public int CartId { get; set; }

        public int InventoryItemId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        public ShoppingCart Cart { get; set; }

        public InventoryItem InventoryItem { get; set; }
    }
}
