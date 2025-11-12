using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using test1.Models;
using test1.Models.AccessModel;
using test1.Models.Authentication;
using test1.Service;

namespace test1.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class CartAPIController : ControllerBase
	{
		// Sử dụng dependency injection cho QlbanQuanAoContext nếu có thể,
		// nếu không, bạn có thể khởi tạo như dưới đây (tuy nhiên nên đăng ký trong DI container)
		private readonly QlbanQuanAoContext _context;
		private readonly JwtService _jwtService;

		public CartAPIController(JwtService jwtService, QlbanQuanAoContext context)
		{
			_jwtService = jwtService;
			_context = context;
		}

		// GET: api/cart
		[HttpGet]
		public IActionResult GetCart()
		{
			var cart = GetCartFromDatabase();
			return Ok(cart); // Trả về JSON với danh sách sản phẩm trong giỏ hàng
		}

		// POST: api/cart/AddCart?id=...&NumberOfProducts=...&color=...&price=...
		[HttpPost("AddCart")]
		public IActionResult AddCart(int id, int NumberOfProducts, string color, float price)
		{
			var customerId = GetCurrentCustomerId();
			if (customerId == -1)
			{
				return Unauthorized(new { message = "Bạn chưa đăng nhập hoặc token không hợp lệ." });
			}

			// Kiểm tra xem người dùng đã có địa chỉ giao hàng mặc định chưa
			var defaultShippingAddress = _context.ShippingAddresses
				.FirstOrDefault(e => e.UserId == customerId && e.IsDefault == true);

			if (defaultShippingAddress == null)
			{
				return BadRequest(new { message = "Bạn chưa có địa chỉ giao hàng mặc định. Vui lòng cập nhật trong phần 'My Account'." });
			}

			var order = _context.Orders
				.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == false);

			var customer = _context.Users.FirstOrDefault(o => o.Id == customerId);
			if (order == null)
			{
				order = new Order
				{
					UserId = customerId,
					Fullname = customer?.Fullname,
					PhoneNumber = customer?.PhoneNumber,
					Active = true,
					OrderDate = DateTime.Now,
					Address = "Địa chỉ mặc định",
					IsQuickPurchase = false
				};
				_context.Orders.Add(order);
				_context.SaveChanges();
			}

			var orderDetail = _context.OrderDetails
				.FirstOrDefault(od => od.OrderId == order.Id && od.ProductId == id);

			if (orderDetail == null)
			{
				orderDetail = new OrderDetail
				{
					OrderId = order.Id,
					ProductId = id,
					NumberOfProducts = NumberOfProducts,
					Price = price
				};
				_context.OrderDetails.Add(orderDetail);
			}
			else
			{
				orderDetail.NumberOfProducts += NumberOfProducts;
				orderDetail.Price = price;
			}
			_context.SaveChanges();
			return Ok(new { message = "Thêm vào giỏ hàng thành công!" });
		}

		// PATCH: api/cart/increase/{id}
		[HttpPatch("increase/{id}")]
		public IActionResult Increase(int id)
		{
			var cart = GetCartFromDatabase();
			var item = cart.FirstOrDefault(x => x.Id == id);
			if (item != null)
			{
				item.NumberOfProducts += 1;
				_context.SaveChanges();
				return Ok(item);
			}
			return NotFound(new { message = "Sản phẩm không tồn tại trong giỏ hàng." });
		}

		// PATCH: api/cart/decrease/{id}
		[HttpPatch("decrease/{id}")]
		public IActionResult Decrease(int id)
		{
			var cart = GetCartFromDatabase();
			var item = cart.FirstOrDefault(x => x.Id == id);
			if (item != null && item.NumberOfProducts > 1)
			{
				item.NumberOfProducts -= 1;
				_context.SaveChanges();
				return Ok(item);
			}
			return BadRequest(new { message = "Không thể giảm số lượng sản phẩm dưới 1." });
		}

		// DELETE: api/cart/{id}
		[HttpDelete("{id}")]
		public IActionResult Delete(int id)
		{
			var cart = GetCartFromDatabase();
			var item = cart.FirstOrDefault(x => x.Id == id);
			if (item != null)
			{
				_context.OrderDetails.Remove(item);
				_context.SaveChanges();
				return Ok(new { message = "Sản phẩm đã được xóa khỏi giỏ hàng." });
			}
			return NotFound(new { message = "Sản phẩm không tồn tại trong giỏ hàng." });
		}

		/// <summary>
		/// Lấy giỏ hàng từ cơ sở dữ liệu dựa trên UserId của khách hàng hiện tại.
		/// </summary>
		/// <returns>Danh sách OrderDetail</returns>
		private List<OrderDetail> GetCartFromDatabase()
		{
			var customerId = GetCurrentCustomerId();
			if (customerId == -1)
			{
				return new List<OrderDetail>();
			}

			int orderId = _context.Orders
				.Where(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == false)
				.Select(o => o.Id)
				.FirstOrDefault();

			if (orderId == 0)
			{
				return new List<OrderDetail>();
			}

			return _context.OrderDetails
				.Where(od => od.OrderId == orderId)
				.ToList();
		}

		/// <summary>
		/// Lấy UserId của khách hàng hiện tại từ access token được lưu trong cookie.
		/// Nếu không có token hoặc token không hợp lệ, trả về -1.
		/// </summary>
		/// <returns>UserId của khách hàng hoặc -1 nếu không hợp lệ</returns>
		private int GetCurrentCustomerId()
		{
			// Cố gắng lấy token từ cookie
			var token = Request.Cookies["accessToken"];

			// Nếu không có token trong cookie, thử lấy từ header
			if (string.IsNullOrEmpty(token))
			{
				var authHeader = Request.Headers["Authorization"].ToString();
				if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
				{
					token = authHeader.Substring("Bearer ".Length).Trim();
				}
			}

			if (string.IsNullOrEmpty(token))
			{
				return -1;  // Không tìm thấy token
			}

			UserId userData = _jwtService.GetUserDataFromToken(token);
			if (userData == null)
			{
				return -1;  // Token không hợp lệ
			}

			var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == userData.PhoneNumber);
			if (user == null)
			{
				return -1;  // Không tìm thấy người dùng
			}

			return user.Id;  // Trả về Id người dùng
		}

		/// <summary>
		/// Lấy danh sách sản phẩm trong giỏ hàng của khách hàng hiện tại.
		/// </summary>
		/// <returns>Danh sách sản phẩm (Cart)</returns>
		[HttpGet("cart")]
		public IActionResult GetCartProduct()
		{
			var customerId = GetCurrentCustomerId();
			if (customerId == -1)
			{
				return Unauthorized(new { message = "Token không hợp lệ hoặc chưa đăng nhập" });
			}

			int orderId = _context.Orders
				.Where(c => c.UserId == customerId && c.Active == true && c.IsQuickPurchase == false)
				.Select(o => o.Id)
				.FirstOrDefault();

			var cartItems = (from a in _context.Products
							 join b in _context.OrderDetails on a.Id equals b.ProductId
							 where b.OrderId == orderId
							 select new
							 {
								 Name = a.Name,
								 ProductId = a.Id,
								 Id = b.Id,
								 OrderId = b.OrderId,
								 Image = a.Image,
								 Price = a.Price,
								 NumberOfProducts = b.NumberOfProducts
							 }).ToList();

			return Ok(cartItems);
		}

		/// <summary>
		/// Mua các sản phẩm đã được chọn.
		/// </summary>
		/// <param name="selectedItems">Danh sách các OrderDetail.Id được chọn</param>
		/// <returns>Thông tin đơn hàng và các sản phẩm đã chọn</returns>
		//[HttpPost("PurchaseSelectedItems")]
		//public IActionResult PurchaseSelectedItems([FromBody] int[] selectedItems)
		//{
		//	// Kiểm tra thông tin đăng nhập của khách hàng
		//	var customerId = GetCurrentCustomerId();
		//	if (customerId == -1)
		//	{
		//		return Unauthorized(new { message = "Token không hợp lệ hoặc chưa đăng nhập" });
		//	}

		//	// Kiểm tra địa chỉ giao hàng mặc định của khách hàng
		//	var defaultShippingAddress = _context.ShippingAddresses
		//		.FirstOrDefault(e => e.UserId == customerId && e.IsDefault == true);
		//	if (defaultShippingAddress == null)
		//	{
		//		return BadRequest(new { message = "Bạn chưa có địa chỉ giao hàng mặc định. Vui lòng cập nhật trong phần 'My Account'." });
		//	}

		//	// Lấy đơn hàng giỏ hàng đang active (không phải quick purchase)
		//	var activeCartOrder = _context.Orders
		//		.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == false);
		//	if (activeCartOrder == null)
		//	{
		//		return BadRequest(new { message = "Không tìm thấy đơn hàng giỏ hàng đang hoạt động." });
		//	}

		//	// Lấy các chi tiết đơn hàng được chọn từ giỏ hàng
		//	var cartItems = _context.OrderDetails
		//		.Where(od => od.OrderId == activeCartOrder.Id && selectedItems.Contains(od.Id))
		//		.ToList();
		//	if (!cartItems.Any())
		//	{
		//		return BadRequest(new { message = "Không có sản phẩm nào được chọn trong giỏ hàng." });
		//	}

		//	// Xóa đơn hàng quick purchase cũ nếu có
		//	var oldQuickOrder = _context.Orders
		//		.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == true);
		//	if (oldQuickOrder != null)
		//	{
		//		_context.OrderDetails.RemoveRange(_context.OrderDetails.Where(od => od.OrderId == oldQuickOrder.Id));
		//		_context.Orders.Remove(oldQuickOrder);
		//		_context.SaveChanges();
		//	}

		//	// Tạo đơn hàng "Mua ngay" mới
		//	var quickPurchaseOrder = new Order
		//	{
		//		UserId = customerId,
		//		Active = true,
		//		IsQuickPurchase = true,
		//		// Các giá trị tạm thời, sẽ được cập nhật trong quá trình Checkout
		//		Address = "loading",
		//		PhoneNumber = "loading",
		//		OrderDate = DateTime.Now,
		//		Status = "pending",
		//		TotalMoney = 0
		//	};
		//	_context.Orders.Add(quickPurchaseOrder);
		//	_context.SaveChanges();

		//	// Thêm các sản phẩm được chọn vào đơn hàng "Mua ngay"
		//	double totalMoney = 0;
		//	foreach (var item in cartItems)
		//	{
		//		var newOrderDetail = new OrderDetail
		//		{
		//			OrderId = quickPurchaseOrder.Id,
		//			ProductId = item.ProductId,
		//			Price = item.Price,
		//			NumberOfProducts = item.NumberOfProducts,
		//			TotalMoney = item.Price * item.NumberOfProducts
		//		};
		//		totalMoney +=(newOrderDetail.TotalMoney ?? 0);

		//		_context.OrderDetails.Add(newOrderDetail);
		//	}
		//	quickPurchaseOrder.TotalMoney = totalMoney;
		//	_context.SaveChanges();

		//	// (Tùy chọn) Xóa các sản phẩm được chuyển khỏi đơn hàng giỏ hàng ban đầu
		//	foreach (var item in cartItems)
		//	{
		//		_context.OrderDetails.Remove(item);
		//	}
		//	_context.SaveChanges();

		//	// Lấy lại thông tin chi tiết sản phẩm của đơn hàng "Mua ngay" để trả về client
		//	var itemsToPurchase = (from od in _context.OrderDetails
		//						   join p in _context.Products on od.ProductId equals p.Id
		//						   where od.OrderId == quickPurchaseOrder.Id
		//						   select new
		//						   {
		//							   Name = p.Name,
		//							   ProductId = p.Id,
		//							   Id = od.Id,
		//							   OrderId = od.OrderId,
		//							   Image = p.Image,
		//							   Price = p.Price,
		//							   NumberOfProducts = od.NumberOfProducts
		//						   }).ToList();

		//	return Ok(new
		//	{
		//		orderId = quickPurchaseOrder.Id,
		//		message = "Các sản phẩm được chọn đã được chuyển sang đơn hàng mua ngay.",
		//		items = itemsToPurchase
		//	});
		//}

		/// <summary>
		/// Thực hiện mua ngay sản phẩm được chọn.
		/// </summary>
		/// <param name="id">Mã sản phẩm</param>
		/// <param name="NumberOfProducts">Số lượng mua</param>
		/// <param name="color">Màu sắc (nếu có)</param>
		/// <param name="price">Giá của sản phẩm</param>
		/// <returns>Thông tin đơn hàng mua ngay</returns>
		[HttpGet("buynow")]
		public IActionResult BuyNow([FromQuery] int id, [FromQuery] int NumberOfProducts, [FromQuery] string color, [FromQuery] float price)
		{
			var customerId = GetCurrentCustomerId();
			if (customerId == -1)
			{
				return Unauthorized(new { message = "Token không hợp lệ hoặc chưa đăng nhập" });
			}

			var defaultShippingAddress = _context.ShippingAddresses
												  .FirstOrDefault(e => e.UserId == customerId && e.IsDefault == true);
			if (defaultShippingAddress == null)
			{
				return BadRequest(new { message = "Bạn chưa có địa chỉ giao hàng mặc định. Vui lòng cập nhật trong phần 'My Account'." });
			}

			// Kiểm tra và xóa đơn hàng "Mua ngay" cũ nếu có
			var quickPurchaseOrder = _context.Orders
				.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == true);

			if (quickPurchaseOrder != null)
			{
				_context.OrderDetails.RemoveRange(_context.OrderDetails.Where(od => od.OrderId == quickPurchaseOrder.Id));
				_context.Orders.Remove(quickPurchaseOrder);
				_context.SaveChanges();
			}

			// Tạo đơn hàng "Mua ngay" mới
			quickPurchaseOrder = new Order
			{
				UserId = customerId,
				Active = true,
				Address = "loading",    // Giá trị tạm thời, cần được cập nhật trong quá trình Checkout
				PhoneNumber = "loading",  // Giá trị tạm thời, cần được cập nhật
				IsQuickPurchase = true
			};
			_context.Orders.Add(quickPurchaseOrder);
			_context.SaveChanges();

			// Thêm sản phẩm vào đơn hàng "Mua ngay"
			var orderDetail = new OrderDetail
			{
				OrderId = quickPurchaseOrder.Id,
				ProductId = id,
				NumberOfProducts = NumberOfProducts,
				// Nếu có thuộc tính Color, có thể mở dòng dưới đây:
				// Color = color,
				Price = price
			};
			_context.OrderDetails.Add(orderDetail);
			_context.SaveChanges();

			// Trả về thông tin đơn hàng "Mua ngay" để client thực hiện bước tiếp theo (ví dụ: Checkout)
			return Ok(new { orderId = quickPurchaseOrder.Id, message = "Đơn hàng mua ngay được tạo thành công" });
		}

	}
}
