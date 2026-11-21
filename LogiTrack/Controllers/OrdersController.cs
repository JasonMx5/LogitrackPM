using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LogiTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack.Controllers
{
    [Route("api/orders")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly LogiTrackContext _context;

        public OrdersController(LogiTrackContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetAll()
        {
            return await _context.Orders.ToListAsync();
        }

        [HttpGet("{id}", Name = "GetOrderById")]
        public async Task<ActionResult<Order>> GetById(int id)
        {
            var order = await _context.Orders
                .Include(candidate => candidate.Items)
                .FirstOrDefaultAsync(candidate => candidate.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            foreach (var item in order.Items)
            {
                item.Order = null;
            }

            return order;
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Post([FromBody] Order order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return CreatedAtRoute("GetOrderById", new { id = order.OrderId }, null);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _context.Orders
                .Include(candidate => candidate.Items)
                .FirstOrDefaultAsync(candidate => candidate.OrderId == id);
            if (order == null)
            {
                return NotFound();
            }

            foreach (var item in order.Items)
            {
                item.OrderId = null;
                item.Order = null;
            }

            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}