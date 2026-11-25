using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers
{
    [Route("api/cart")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private static readonly TimeSpan CartLifetime = TimeSpan.FromDays(30);

        private readonly LogiTrackContext _context;

        public CartController(LogiTrackContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var cart = await GetActiveCartAsync(userId, createIfMissing: true);
            return Ok(ToResponse(cart));
        }

        [HttpPost("items")]
        public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var inventoryItem = await _context.InventoryItems
                .FirstOrDefaultAsync(item => item.ItemId == request.InventoryItemId);
            if (inventoryItem == null)
            {
                return NotFound(new { message = "Inventory item not found." });
            }

            var cart = await GetActiveCartAsync(userId, createIfMissing: true);
            var cartItem = cart.Items.FirstOrDefault(
                item => item.InventoryItemId == request.InventoryItemId);
            var newQuantity = (long)(cartItem == null ? 0 : cartItem.Quantity) + request.Quantity;
            if (newQuantity > inventoryItem.Quantity)
            {
                return Conflict(new
                {
                    message = "Requested quantity exceeds available inventory.",
                    availableQuantity = inventoryItem.Quantity
                });
            }

            if (cartItem == null)
            {
                cartItem = new ShoppingCartItem
                {
                    InventoryItemId = inventoryItem.ItemId,
                    InventoryItem = inventoryItem,
                    Quantity = request.Quantity
                };
                cart.Items.Add(cartItem);
            }
            else
            {
                cartItem.Quantity = (int)newQuantity;
            }

            cart.ExpiresAt = DateTime.UtcNow.Add(CartLifetime);
            await _context.SaveChangesAsync();

            return Ok(ToResponse(cart));
        }

        [HttpDelete("items/{inventoryItemId}")]
        public async Task<IActionResult> RemoveItem(int inventoryItemId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var cart = await GetActiveCartAsync(userId, createIfMissing: false);
            if (cart == null)
            {
                return NotFound();
            }

            var cartItem = cart.Items.FirstOrDefault(
                item => item.InventoryItemId == inventoryItemId);
            if (cartItem == null)
            {
                return NotFound();
            }

            cart.Items.Remove(cartItem);
            cart.ExpiresAt = DateTime.UtcNow.Add(CartLifetime);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<ShoppingCart> GetActiveCartAsync(string userId, bool createIfMissing)
        {
            var cart = await _context.ShoppingCarts
                .Include(candidate => candidate.Items)
                .ThenInclude(item => item.InventoryItem)
                .FirstOrDefaultAsync(candidate => candidate.UserId == userId);

            if (cart != null && cart.ExpiresAt <= DateTime.UtcNow)
            {
                _context.ShoppingCarts.Remove(cart);
                await _context.SaveChangesAsync();
                cart = null;
            }

            if (cart == null && createIfMissing)
            {
                cart = new ShoppingCart
                {
                    UserId = userId,
                    ExpiresAt = DateTime.UtcNow.Add(CartLifetime)
                };
                _context.ShoppingCarts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        private static object ToResponse(ShoppingCart cart)
        {
            return new
            {
                cartId = cart.CartId,
                expiresAt = cart.ExpiresAt,
                items = cart.Items.Select(item => new
                {
                    inventoryItemId = item.InventoryItemId,
                    name = item.InventoryItem.Name,
                    quantity = item.Quantity,
                    availableQuantity = item.InventoryItem.Quantity,
                    location = item.InventoryItem.Location
                }).ToList()
            };
        }
    }

    public class AddCartItemRequest
    {
        [Range(1, int.MaxValue)]
        public int InventoryItemId { get; set; }

        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }
}
