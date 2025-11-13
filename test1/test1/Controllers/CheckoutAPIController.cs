
﻿using Microsoft.AspNetCore.Mvc;
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
                .Include(od => od.ProductVariant) // ADD THIS LINE
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
                    TotalMoney = item.Price * item.NumberOfProducts,
                    ProductVariantId = item.ProductVariantId // ADD THIS LINE - COPY THE VARIANT ID
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
                                join pv in _context.ProductVariants on b.ProductVariantId equals pv.Id into pvGroup
                                from pv in pvGroup.DefaultIfEmpty()
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
                                    ProductVariantId = b.ProductVariantId,
                                    Size = pv != null ? pv.Size : null,
                                    Color = pv != null ? pv.Color : null
                                }).ToList();

            // ADD DEBUG LOGGING
            Console.WriteLine("=== BACKEND CHECKOUT DEBUG ===");
            foreach (var item in checkoutData)
            {
                Console.WriteLine($"Item: {item.Name}, VariantId: {item.ProductVariantId}, Size: {item.Size}, Color: {item.Color}");
            }
            Console.WriteLine("=== END BACKEND DEBUG ===");

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
                return Unauthorized(new { message = "Token không hợp lệ hoặc người dùng chưa đăng nhập" });
            }

            var checkoutData = (from a in _context.Products
                                join b in _context.OrderDetails on a.Id equals b.ProductId
                                join c in _context.Orders on b.OrderId equals c.Id
                                join d in _context.Users on c.UserId equals d.Id
                                join e in _context.ShippingAddresses on d.Id equals e.UserId
                                join pv in _context.ProductVariants on b.ProductVariantId equals pv.Id into pvGroup
                                from pv in pvGroup.DefaultIfEmpty() // LEFT JOIN for variants
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
                                    // ADD VARIANT INFORMATION
                                    ProductVariantId = b.ProductVariantId,
                                    Size = pv != null ? pv.Size : null,
                                    Color = pv != null ? pv.Color : null
                                }).ToList();

            if (orderId <= 0)
            {
                return BadRequest(new { message = "orderId không hợp lệ" });
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
                    return Unauthorized(new { message = "Token không hợp lệ hoặc người dùng chưa đăng nhập" });
                }

                var activeOrder = _context.Orders
                    .FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.Address == "loading" && o.IsQuickPurchase == false);

                if (activeOrder != null)
                {
                    var orderDetails = _context.OrderDetails
                        .Where(od => od.OrderId == activeOrder.Id)
                        .Include(od => od.ProductVariant)
                        .ToList();

                    var stockValidationErrors = new List<string>();

                    foreach (var orderDetail in orderDetails)
                    {
                        if (!orderDetail.NumberOfProducts.HasValue || orderDetail.NumberOfProducts.Value <= 0)
                        {
                            stockValidationErrors.Add($"Số lượng không hợp lệ");
                            //stockValidationErrors.Add($"Số lượng không hợp lệ OrderDetail ID: {orderDetail.Id}");
                            continue;
                        }

                        int requestedQuantity = orderDetail.NumberOfProducts.Value;

                        // FIX: Require product variant
                        if (!orderDetail.ProductVariantId.HasValue)
                        {
                            var productName = _context.Products
                                .Where(p => p.Id == orderDetail.ProductId)
                                .Select(p => p.Name)
                                .FirstOrDefault() ?? "Sản phẩm không rõ";
                            stockValidationErrors.Add($"{productName}: Vui lòng chọn size và màu sắc");
                            continue;
                        }

                        var productVariant = orderDetail.ProductVariant;
                        if (productVariant == null)
                        {
                            stockValidationErrors.Add($"Không tìm thấy biến thể sản phẩm");
                            //stockValidationErrors.Add($"Product variant not found for OrderDetail ID: {orderDetail.Id}");
                            continue;
                        }

                        // FIX: Verify variant has both size and color
                        if (string.IsNullOrEmpty(productVariant.Size) || string.IsNullOrEmpty(productVariant.Color))
                        {
                            var productName = _context.Products
                                .Where(p => p.Id == orderDetail.ProductId)
                                .Select(p => p.Name)
                                .FirstOrDefault() ?? "Sản phẩm không rõ";
                            stockValidationErrors.Add($"{productName}: Biến thể sản phẩm không đầy đủ thông tin");
                            continue;
                        }

                        if (productVariant.StockQuantity < requestedQuantity)
                        {
                            var productName = _context.Products
                                .Where(p => p.Id == orderDetail.ProductId)
                                .Select(p => p.Name)
                                .FirstOrDefault() ?? "Sản phẩm không rõ";

                            stockValidationErrors.Add(
                                $"{productName} ({productVariant.Color}, {productVariant.Size}): " +
                                $"Cần mua: {requestedQuantity}, Tồn kho: {productVariant.StockQuantity}"
                            );
                        }
                    }

                    if (stockValidationErrors.Any())
                    {
                        return BadRequest(new
                        {
                            error = "Không thể tạo đơn",
                            details = stockValidationErrors
                        });
                    }

                    // FIX: Update stock quantities for all products with valid variants
                    foreach (var orderDetail in orderDetails)
                    {
                        if (orderDetail.NumberOfProducts.HasValue &&
                            orderDetail.ProductVariantId.HasValue &&
                            orderDetail.ProductVariant != null &&
                            !string.IsNullOrEmpty(orderDetail.ProductVariant.Size) &&
                            !string.IsNullOrEmpty(orderDetail.ProductVariant.Color))
                        {
                            int quantityToDeduct = orderDetail.NumberOfProducts.Value;
                            orderDetail.ProductVariant.StockQuantity -= quantityToDeduct;

                            // Ensure stock doesn't go negative (validation should prevent this)
                            if (orderDetail.ProductVariant.StockQuantity < 0)
                                orderDetail.ProductVariant.StockQuantity = 0;
                        }
                    }

                    // Update order information
                    activeOrder.Fullname = orderRequest.Fullname;
                    activeOrder.PhoneNumber = orderRequest.PhoneNumber;
                    activeOrder.Address = orderRequest.Address;
                    activeOrder.PaymentMethod = orderRequest.PaymentMethod;
                    activeOrder.OrderDate = DateTime.Now;
                    activeOrder.TotalMoney = orderRequest.TotalMoney;
                    activeOrder.Status = "processing";
                    activeOrder.Active = false;

                    _context.SaveChanges();

                    // Remove items from original cart
                    if (orderRequest.SelectedItems != null && orderRequest.SelectedItems.Any())
                    {
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

                    return Ok(new { message = "Đặt hàng thành công", orderId = activeOrder.Id });
                }
                else
                {
                    return BadRequest(new { error = "Không tìm thấy đơn hàng giỏ hàng đang hoạt động" });
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
                    return Unauthorized(new { message = "Token không hợp lệ hoặc người dùng chưa đăng nhập" });
                }

                var quickPurchaseOrder = _context.Orders
                    .FirstOrDefault(o => o.UserId == customerId && o.Active == true && o.IsQuickPurchase == true);

                if (quickPurchaseOrder != null)
                {
                    var orderDetails = _context.OrderDetails
                        .Where(od => od.OrderId == quickPurchaseOrder.Id)
                        .Include(od => od.ProductVariant)
                        .ToList();

                    var stockValidationErrors = new List<string>();
                    foreach (var orderDetail in orderDetails)
                    {
                        if (!orderDetail.NumberOfProducts.HasValue || orderDetail.NumberOfProducts.Value <= 0)
                        {
                            stockValidationErrors.Add($"Số lượng không phù hợp");
                            //stockValidationErrors.Add($"Invalid quantity for OrderDetail ID: {orderDetail.Id}");
                            continue;
                        }

                        int requestedQuantity = orderDetail.NumberOfProducts.Value;

                        // FIX: Require product variant
                        if (!orderDetail.ProductVariantId.HasValue)
                        {
                            var productName = _context.Products
                                .Where(p => p.Id == orderDetail.ProductId)
                                .Select(p => p.Name)
                                .FirstOrDefault() ?? "Sản phẩm không rõ";
                            stockValidationErrors.Add($"{productName}: Vui lòng chọn size và màu sắc");
                            continue;
                        }

                        if (orderDetail.ProductVariantId.HasValue && orderDetail.ProductVariant != null)
                        {
                            // FIX: Verify variant has both size and color
                            if (string.IsNullOrEmpty(orderDetail.ProductVariant.Size) || string.IsNullOrEmpty(orderDetail.ProductVariant.Color))
                            {
                                var productName = _context.Products
                                    .Where(p => p.Id == orderDetail.ProductId)
                                    .Select(p => p.Name)
                                    .FirstOrDefault() ?? "Sản phẩm không rõ";
                                stockValidationErrors.Add($"{productName}: Biến thể sản phẩm không đầy đủ thông tin");
                                continue;
                            }

                            if (orderDetail.ProductVariant.StockQuantity < requestedQuantity)
                            {
                                var productName = _context.Products
                                    .Where(p => p.Id == orderDetail.ProductId)
                                    .Select(p => p.Name)
                                    .FirstOrDefault() ?? "Sản phẩm không rõ";

                                stockValidationErrors.Add(
                                    $"{productName} ({orderDetail.ProductVariant.Color}, {orderDetail.ProductVariant.Size}): " +
                                    $"Cần mua: {requestedQuantity}, Tồn kho: {orderDetail.ProductVariant.StockQuantity}"
                                );
                            }
                        }
                    }

                    if (stockValidationErrors.Any())
                    {
                        return BadRequest(new
                        {
                            error = "Một số sản phẩm không đủ hàng trong kho",
                            details = stockValidationErrors
                        });
                    }

                    // FIX: Update stock
                    foreach (var orderDetail in orderDetails)
                    {
                        if (orderDetail.NumberOfProducts.HasValue &&
                            orderDetail.ProductVariantId.HasValue &&
                            orderDetail.ProductVariant != null &&
                            !string.IsNullOrEmpty(orderDetail.ProductVariant.Size) &&
                            !string.IsNullOrEmpty(orderDetail.ProductVariant.Color))
                        {
                            int quantityToDeduct = orderDetail.NumberOfProducts.Value;
                            orderDetail.ProductVariant.StockQuantity -= quantityToDeduct;
                            if (orderDetail.ProductVariant.StockQuantity < 0)
                                orderDetail.ProductVariant.StockQuantity = 0;
                        }
                    }

                    quickPurchaseOrder.PaymentMethod = orderRequest.PaymentMethod;
                    quickPurchaseOrder.Active = false;
                    quickPurchaseOrder.Status = "processing";
                    quickPurchaseOrder.Fullname = orderRequest.Fullname;
                    quickPurchaseOrder.Address = orderRequest.Address;
                    quickPurchaseOrder.TotalMoney = orderRequest.TotalMoney;
                    quickPurchaseOrder.PhoneNumber = orderRequest.PhoneNumber;
                    quickPurchaseOrder.OrderDate = DateTime.Now;

                    _context.SaveChanges();
                    return Ok(new { message = "Đặt hàng thành công", orderId = quickPurchaseOrder.Id });
                }

                return BadRequest(new { error = "Không tìm thấy đơn hàng mua ngay đang hoạt động" });
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


        [HttpPost("validate-stock")]
        public IActionResult ValidateStock([FromBody] StockValidationRequest request)
        {
            try
            {
                var customerId = GetCurrentCustomerId();
                if (customerId == -1)
                {
                    return Unauthorized(new { message = "Token không hợp lệ hoặc người dùng chưa đăng nhập." });
                }

                var orderDetails = _context.OrderDetails
                    .Where(od => od.OrderId == request.OrderId)
                    .Include(od => od.Product)
                    .Include(od => od.ProductVariant)
                    .ToList();

                var validationErrors = new List<string>();
                var isValid = true;

                foreach (var orderDetail in orderDetails)
                {
                    string productName = orderDetail.Product?.Name ?? "Unknown Product";

                    // Check quantity
                    if (!orderDetail.NumberOfProducts.HasValue || orderDetail.NumberOfProducts.Value <= 0)
                    {
                        validationErrors.Add($"{productName}: Số lượng không hợp lệ");
                        isValid = false;
                        continue;
                    }

                    int requestedQuantity = orderDetail.NumberOfProducts.Value;

                    // FIX: Require product variant for all products
                    if (!orderDetail.ProductVariantId.HasValue)
                    {
                        validationErrors.Add($"{productName}: Vui lòng chọn size và màu sắc");
                        isValid = false;
                        continue;
                    }

                    // Check product variant stock
                    var productVariant = orderDetail.ProductVariant;
                    if (productVariant == null)
                    {
                        validationErrors.Add($"{productName}: Không tìm thấy biến thể sản phẩm");
                        isValid = false;
                        continue;
                    }

                    // FIX: Verify variant has both size and color
                    if (string.IsNullOrEmpty(productVariant.Size) || string.IsNullOrEmpty(productVariant.Color))
                    {
                        validationErrors.Add($"{productName}: Biến thể sản phẩm không đầy đủ thông tin");
                        isValid = false;
                        continue;
                    }

                    int availableStock = productVariant.StockQuantity;

                    if (availableStock < requestedQuantity)
                    {
                        validationErrors.Add(
                            $"{productName} ({productVariant.Color}, {productVariant.Size}): " +
                            $"Cần mua: {requestedQuantity}, Tồn kho: {availableStock}"
                        );
                        isValid = false;
                    }
                }

                return Ok(new
                {
                    isValid = isValid,
                    errors = validationErrors
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    isValid = false,
                    errors = new List<string> { "Lỗi kiểm tra tồn kho: " + ex.Message }
                });
            }
        }
    }
}