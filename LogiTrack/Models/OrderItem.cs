using System.ComponentModel.DataAnnotations;

namespace LogiTrack.Models
{
    public class OrderItem
    {
        [Key]
        public int OrderItemId { get; set; }

        public int OrderId { get; set; }

        public int InventoryItemId { get; set; }

        [Required]
        public string Name { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        public string Location { get; set; }

        public Order Order { get; set; }
    }
}
