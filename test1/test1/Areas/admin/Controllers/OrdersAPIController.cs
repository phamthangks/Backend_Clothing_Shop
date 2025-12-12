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
	public IActionResult GetAll(int page = 1, int size = 10, string? search = null, string? status = null, string? paymentMethod = null)
	{
		var query = _context.Orders.AsQueryable();

		// Search by fullname, phone number, or address
		if (!string.IsNullOrWhiteSpace(search))
		{
			query = query.Where(o => 
				o.Fullname.Contains(search) || 
				o.PhoneNumber.Contains(search) || 
				o.Address.Contains(search));
		}

		// Filter by status
		if (!string.IsNullOrWhiteSpace(status))
		{
			query = query.Where(o => o.Status == status);
		}

		// Filter by payment method
		if (!string.IsNullOrWhiteSpace(paymentMethod))
		{
			query = query.Where(o => o.PaymentMethod == paymentMethod);
		}

		var total = query.Count();

		var orders = query
			.OrderByDescending(o => o.OrderDate)
			.Skip((page - 1) * size)
			.Take(size)
			.ToList();

		return Ok(new
		{
			data = orders,
			total = total,
			page = page,
			size = size
		});
	}

	[HttpGet("{id}")]
	public IActionResult GetById(int id)
	{
		var order = _context.Orders
			.Include(o => o.OrderDetails)
				.ThenInclude(od => od.Product)
					.ThenInclude(p => p.ProductImages)
			.Include(o => o.OrderDetails)
				.ThenInclude(od => od.ProductVariant)
			.Include(o => o.User)
			.FirstOrDefault(o => o.Id == id);

		if (order == null)
			return NotFound(new { message = $"Không tìm thấy đơn hàng {id}" });

		return Ok(order);
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

		[HttpGet("statistics")]
		public IActionResult GetOrderStatistics()
		{
			// Lấy tất cả đơn hàng
			var allOrders = _context.Orders.ToList();
			
			// Hàm helper để kiểm tra trạng thái (case-insensitive)
			bool MatchesStatus(string? status, string[] statusList)
			{
				if (string.IsNullOrEmpty(status)) return false;
				var statusLower = status.ToLower().Trim();
				return statusList.Any(s => statusLower == s.ToLower().Trim());
			}
			
			// Đếm theo các trạng thái chuẩn (hỗ trợ cả tiếng Việt và tiếng Anh)
			var pendingCount = allOrders.Count(o => 
				MatchesStatus(o.Status, new[] { "pending", "chờ xử lý", "cho xu ly" }));
			
			var processingCount = allOrders.Count(o => 
				MatchesStatus(o.Status, new[] { "processing", "đang xử lý", "dang xu ly" }));
			
			var shippedCount = allOrders.Count(o => 
				MatchesStatus(o.Status, new[] { "shipped", "đang giao", "dang giao", "delivering" }));
			
			var deliveredCount = allOrders.Count(o => 
				MatchesStatus(o.Status, new[] { "delivered", "đã giao", "da giao", "completed" }));
			
			var cancelledCount = allOrders.Count(o => 
				MatchesStatus(o.Status, new[] { "cancelled", "đã hủy", "da huy", "hủy", "huy" }));

			return Ok(new
			{
				pending = pendingCount,
				processing = processingCount,
				shipped = shippedCount,
				delivered = deliveredCount,
				cancelled = cancelledCount
			});
		}
    }
}
