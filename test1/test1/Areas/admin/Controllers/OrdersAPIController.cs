using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Numerics;
using test1.Models;

namespace test1.Areas.Admin.Controllers
{

    [Area("admin")]
    [Route("api/order")]
    [ApiController]
    public class OrdersAPIController : Controller
    {
		private readonly QlbanQuanAoContext _context;
		public OrdersAPIController(QlbanQuanAoContext context)
		{
			_context = context;
		}

		[HttpGet]
		public IActionResult GetAll(int page = 1, int size = 10)
		{
			var orders = _context.Orders
				.Skip((page - 1) * size)
				.Take(size)
				.ToList();

			var total = _context.Orders.Count();

			return Ok(new
			{
				data = orders,
				total = total,
				page = page,
				size = size
			});
		}


		[HttpPut("Edit/{id}")]
		public IActionResult Edit(int id, [FromBody] Order order)
		{
			if (id != order.Id)
				return BadRequest(new { message = "ID không khớp với Order.Id" });

			var existing = _context.Orders.FirstOrDefault(o => o.Id == id);
			if (existing == null)
				return NotFound(new { message = $"Không tìm thấy đơn hàng {id}" });

			existing.Status = order.Status;
			_context.Entry(existing).State = EntityState.Modified;

			_context.SaveChanges();

			return Ok(existing);
		}



		[HttpDelete("Delete/{id}")]
        public IActionResult Delete(int id)
        {
            var order = _context.Orders.Include(o => o.OrderDetails)
                                  .FirstOrDefault(o => o.Id == id);
            if (order == null)
            {
                return NotFound(new { message = "Không tìm thấy đơn hàng" });
            }

            _context.OrderDetails.RemoveRange(order.OrderDetails);

            _context.Orders.Remove(order);
            _context.SaveChanges();

            return Ok(new { message = "Đơn hàng đã được xóa thành công" });
        }
    }
}
