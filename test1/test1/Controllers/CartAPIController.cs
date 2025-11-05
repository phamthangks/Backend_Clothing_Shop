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
		
		private readonly QlbanQuanAoContext _context;
		private readonly JwtService _jwtService;

		public CartAPIController(JwtService jwtService, QlbanQuanAoContext context)
		{
			_jwtService = jwtService;
			_context = context;
		}

		
		[HttpGet]
		public IActionResult GetCart()
		{
			var cart = GetCartFromDatabase();
			return Ok(cart); 
		}


        [HttpPost("AddCart")]
        public IActionResult AddCart(int id, int NumberOfProducts, int? productVariantId, float price)
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

            // FIX: Require product variant
            if (!productVariantId.HasValue)
            {
                return BadRequest(new { message = "Vui lòng chọn size và màu sắc cho sản phẩm." });
            }

            // FIX: Verify the product variant exists and has both size and color
            var productVariant = _context.ProductVariants
                .FirstOrDefault(pv => pv.Id == productVariantId.Value && pv.ProductId == id);

            if (productVariant == null)
            {
                return BadRequest(new { message = "Phiên bản sản phẩm không tồn tại." });
            }

            if (string.IsNullOrEmpty(productVariant.Size) || string.IsNullOrEmpty(productVariant.Color))
            {
                return BadRequest(new { message = "Phiên bản sản phẩm không đầy đủ thông tin size và màu sắc." });
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
                .FirstOrDefault(od => od.OrderId == order.Id && od.ProductId == id && od.ProductVariantId == productVariantId);

            if (orderDetail == null)
            {
                orderDetail = new OrderDetail
                {
                    OrderId = order.Id,
                    ProductId = id,
                    NumberOfProducts = NumberOfProducts,
                    Price = price,
                    ProductVariantId = productVariantId
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
							 join pv in _context.ProductVariants on b.ProductVariantId equals pv.Id into pvGroup
							 from pv in pvGroup.DefaultIfEmpty()
							 where b.OrderId == orderId
							 select new
							 {
								 Name = a.Name,
								 ProductId = a.Id,
								 Id = b.Id,
								 OrderId = b.OrderId,
								 Image = a.Image,
								 Price = a.Price,
								 NumberOfProducts = b.NumberOfProducts,                               //////////////////////////////
								 ProductVariantId = b.ProductVariantId,
								 Size = pv != null ? pv.Size : null,
								 Color = pv != null ? pv.Color : null
							 }).ToList();

			return Ok(cartItems);
		}


        [HttpGet("buynow")]
        public IActionResult BuyNow([FromQuery] int id, [FromQuery] int NumberOfProducts, [FromQuery] int? productVariantId, [FromQuery] float price)
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

            // FIX: Require product variant for BuyNow
            if (!productVariantId.HasValue)
            {
                return BadRequest(new { message = "Vui lòng chọn size và màu sắc cho sản phẩm." });
            }

            // FIX: Verify the product variant exists and has both size and color
            var productVariant = _context.ProductVariants
                .FirstOrDefault(pv => pv.Id == productVariantId.Value && pv.ProductId == id);

            if (productVariant == null)
            {
                return BadRequest(new { message = "Phiên bản sản phẩm không tồn tại." });
            }

            if (string.IsNullOrEmpty(productVariant.Size) || string.IsNullOrEmpty(productVariant.Color))
            {
                return BadRequest(new { message = "Phiên bản sản phẩm không đầy đủ thông tin size và màu sắc." });
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
                Address = "loading",
                PhoneNumber = "loading",
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
                ProductVariantId = productVariantId,
                Price = price
            };
            _context.OrderDetails.Add(orderDetail);
            _context.SaveChanges();

            return Ok(new { orderId = quickPurchaseOrder.Id, message = "Đơn hàng mua ngay được tạo thành công" });
        }

    }
}
