using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using test1.Models;
using test1.Models.AccessModel;
using test1.Models.Authentication;
using test1.Models.CheckoutModel;
using test1.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using test1.Models.AccessModel;
using test1.Models.CheckoutModel;
using test1.Models;
using test1.Service;

namespace Test.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class CheckoutAPIController : ControllerBase
	{
		private readonly QlbanQuanAoContext _context;
		private readonly JwtService _jwtService;

		public CheckoutAPIController(JwtService jwtService, QlbanQuanAoContext context)
		{
			_jwtService = jwtService;
			_context = context;
		}

		/// <summary>
		/// Lưu danh sách các sản phẩm được chọn (OrderDetail.Id) vào Session.
		/// POST: api/CheckoutAPI/store-selected-items
		/// </summary>
		[HttpPost("store-selected-items")]
		public IActionResult StoreSelectedItems([FromBody] List<int> selectedItems)
		{
			HttpContext.Session.SetString("SelectedItems", JsonConvert.SerializeObject(selectedItems));
			return Ok(new { message = "Selected items stored successfully." });
		}

		/// <summary>
		/// Lấy thông tin các sản phẩm cần thanh toán dựa vào orderId và danh sách sản phẩm đã chọn từ Session.
		/// GET: api/CheckoutAPI/checkout/{orderId}
		/// </summary>
		[HttpPost("checkout")]
		public IActionResult Checkout([FromBody] CheckoutRequest request)
		{
			// Kiểm tra thông tin đăng nhập của khách hàng
			var customerId = GetCurrentCustomerId();
			if (customerId == -1)
			{
				return Unauthorized(new { message = "Token không hợp lệ hoặc chưa đăng nhập" });
			}

			// Kiểm tra địa chỉ giao hàng mặc định của khách hàng
			var defaultShippingAddress = _context.ShippingAddresses
				.FirstOrDefault(e => e.UserId == customerId && e.IsDefault == true);
			if (defaultShippingAddress == null)
			{
				return BadRequest(new { message = "Bạn chưa có địa chỉ giao hàng mặc định. Vui lòng cập nhật trong phần 'My Account'." });
			}
			var p = _context.Orders
				.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == false && o.Address == "loading");
			// Nếu đơn hàng có Address là "loading", thì xóa luôn
			if (p != null && p.Address == "loading")
			{
				var oldOrderDetails = _context.OrderDetails.Where(od => od.OrderId == p.Id);
				_context.OrderDetails.RemoveRange(oldOrderDetails); // Xóa chi tiết đơn hàng
				_context.Orders.Remove(p); // Xóa đơn hàng
				_context.SaveChanges();
			}
			// Lấy danh sách sản phẩm được chọn từ Angular
			List<int> selectedItems = request.SelectedItems ?? new List<int>();

			// Lấy đơn hàng giỏ hàng đang active (không phải quick purchase)
			var activeCartOrder = _context.Orders
				.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == false);
			
			if (activeCartOrder == null)
			{
				return BadRequest(new { message = "Không tìm thấy đơn hàng giỏ hàng đang hoạt động." });
			}
			
			// Lấy các chi tiết đơn hàng được chọn từ giỏ hàng
			var cartItems = _context.OrderDetails
				.Where(od => od.OrderId == activeCartOrder.Id && selectedItems.Contains(od.Id))
				.ToList();
			if (!cartItems.Any())
			{
				return BadRequest(new { message = "Không có sản phẩm nào được chọn trong giỏ hàng." });
			}

			// Tạo đơn hàng mới với thông tin mặc định (Address, PhoneNumber là "loading")
			var order = new Order
			{
				UserId = customerId,
				Active = true,
				IsQuickPurchase = false,
				Address = "loading",
				PhoneNumber = "loading",
				OrderDate = DateTime.Now,
				Status = "pending",
				TotalMoney = 0
			};
			_context.Orders.Add(order);
			_context.SaveChanges();

			double totalMoney = 0;
			foreach (var item in cartItems)
			{
				var newOrderDetail = new OrderDetail
				{
					OrderId = order.Id,
					ProductId = item.ProductId,
					Price = item.Price,
					NumberOfProducts = item.NumberOfProducts,
					TotalMoney = item.Price * item.NumberOfProducts
				};
				totalMoney += (newOrderDetail.TotalMoney ?? 0);
				_context.OrderDetails.Add(newOrderDetail);
			}
			order.TotalMoney = totalMoney;
			_context.SaveChanges();


			var checkoutData = (from a in _context.Products
								join b in _context.OrderDetails on a.Id equals b.ProductId
								join c in _context.Orders on b.OrderId equals c.Id
								join d in _context.Users on c.UserId equals d.Id
								join e in _context.ShippingAddresses on d.Id equals e.UserId
								where b.OrderId == order.Id
									  && c.UserId == customerId
									  && e.IsDefault == true
								select new Checkout
								{
									Name = a.Name,
									ProductId = a.Id,
									Id = c.Id,
									Fullname = e.Fullname,
									Price = a.Price,
									NumberOfProducts = b.NumberOfProducts,
									PhoneNumber = e.PhoneNumber,
									Address = e.Address,
									OrderDate = c.OrderDate,
								}).ToList();
			return Ok(new
			{
				orderId = order.Id,
				message = "Các sản phẩm được chọn đã được chuyển sang đơn hàng mua ngay.",
				items = checkoutData
			});
		}

		/// <summary>
		/// Lấy thông tin đơn hàng "Mua ngay" để thanh toán dựa theo orderId.
		/// GET: api/CheckoutAPI/checkout-buynow/{orderId}
		/// </summary>
		[HttpGet("checkout-buynow/{orderId}")]
		public IActionResult Checkout_BuyNow(int orderId)
		{
			var customerId = GetCurrentCustomerId();
			if (customerId == -1)
			{
				return Unauthorized(new { message = "Invalid token or user not authenticated." });
			}

			var checkoutData = (from a in _context.Products
								join b in _context.OrderDetails on a.Id equals b.ProductId
								join c in _context.Orders on b.OrderId equals c.Id
								join d in _context.Users on c.UserId equals d.Id
								join e in _context.ShippingAddresses on d.Id equals e.UserId
								where b.OrderId == orderId
									  && c.UserId == customerId
									  && e.IsDefault == true
								select new Checkout
								{
									Name = a.Name,
									ProductId = a.Id,
									Id = c.Id,
									Fullname = e.Fullname,
									Price = a.Price,
									NumberOfProducts = b.NumberOfProducts,
									PhoneNumber = e.PhoneNumber,
									Address = e.Address,
									OrderDate = c.OrderDate,
								}).ToList();

			if (orderId <= 0)
			{
				return BadRequest(new { message = "Invalid orderId." });
			}
			return Ok(checkoutData);
		}

		/// <summary>
		/// Đặt đơn hàng (thanh toán giỏ hàng) dựa trên thông tin nhận được và các sản phẩm được chọn trong Session.
		/// POST: api/CheckoutAPI/place-order
		/// </summary>
		[HttpPost("place-order")]
		public IActionResult PlaceOrder([FromBody] Checkout orderRequest)
		{
			try
			{
				var customerId = GetCurrentCustomerId();
				if (customerId == -1)
				{
					return Unauthorized(new { message = "Invalid token or user not authenticated." });
				}

				// Tìm đơn hàng đã được tạo ở bước Checkout (đơn hàng có Address = "loading")
				var activeOrder = _context.Orders
					.FirstOrDefault(o =>  o.UserId == customerId && o.Active == true && o.Address == "loading" && o.IsQuickPurchase == false);

				if (activeOrder != null)
				{
					// Cập nhật thông tin đơn hàng dựa trên dữ liệu nhận từ Angular
					activeOrder.Fullname = orderRequest.Fullname;
					activeOrder.PhoneNumber = orderRequest.PhoneNumber;
					activeOrder.Address = orderRequest.Address;
					activeOrder.PaymentMethod = orderRequest.PaymentMethod;
					activeOrder.OrderDate = DateTime.Now;
					activeOrder.TotalMoney = orderRequest.TotalMoney; // Có thể tính lại nếu cần
					activeOrder.Status = "processing";
					activeOrder.Active = false; // Đánh dấu đơn hàng đã được đặt

					_context.SaveChanges();

					// Xóa các sản phẩm được chuyển khỏi đơn hàng giỏ hàng ban đầu
					// Giả sử orderRequest.SelectedItems chứa danh sách các OrderDetail.Id đã chuyển
					if (orderRequest.SelectedItems != null && orderRequest.SelectedItems.Any())
					{
						// Lấy đơn hàng giỏ hàng ban đầu (giả sử đơn hàng này có Address khác "loading")
						var originalCartOrder = _context.Orders
							.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == false && o.Address == "Địa chỉ mặc định");
						if (originalCartOrder != null)
						{
							var itemsToRemove = _context.OrderDetails
								.Where(od => od.OrderId == originalCartOrder.Id && orderRequest.SelectedItems.Contains(od.Id));
							_context.OrderDetails.RemoveRange(itemsToRemove);
							_context.SaveChanges();
						}
					}

					return Ok(new { message = "Order placed successfully.", orderId = activeOrder.Id });
				}
				else
				{
					return BadRequest(new { error = "No active cart order found." });
				}
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = "Internal server error", details = ex.Message });
			}
		}


		/// <summary>
		/// Đặt đơn hàng "Mua ngay" dựa trên thông tin nhận được.
		/// POST: api/CheckoutAPI/place-order-buynow
		/// </summary>
		[HttpPost("place-order-buynow")]
		public IActionResult PlaceOrder_BuyNow([FromBody] Checkout2 orderRequest)
		{
			try
			{
				var customerId = GetCurrentCustomerId();
				if (customerId == -1)
				{
					return Unauthorized(new { message = "Invalid token or user not authenticated." });
				}

				// Tìm đơn hàng "Mua ngay" hiện tại của khách hàng
				var quickPurchaseOrder = _context.Orders
					.FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == true);

				if (quickPurchaseOrder != null)
				{
					quickPurchaseOrder.PaymentMethod = orderRequest.PaymentMethod;
					quickPurchaseOrder.Active = false;
					quickPurchaseOrder.Status = "processing";
					quickPurchaseOrder.Fullname = orderRequest.Fullname;
					quickPurchaseOrder.Address = orderRequest.Address;
					quickPurchaseOrder.TotalMoney = orderRequest.TotalMoney;
					quickPurchaseOrder.PhoneNumber = orderRequest.PhoneNumber;
					quickPurchaseOrder.OrderDate = DateTime.Now;

					_context.SaveChanges();
					return Ok(new { message = "Order placed successfully.", orderId = quickPurchaseOrder.Id });
				}

				return BadRequest(new { error = "No active quick purchase order found." });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = "Internal server error", details = ex.Message });
			}
		}

		/// <summary>
		/// Lấy UserId của khách hàng hiện tại dựa vào access token trong cookie.
		/// Nếu không có token hoặc token không hợp lệ, trả về -1.
		/// </summary>
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
	}
}
