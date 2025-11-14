using Microsoft.AspNetCore.Mvc;
using test1.Models;
using test1.Models.AccessModel;
using test1.Models.AccountModel;
using test1.Models.Authentication;
using test1.Service;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Test.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class MyAccountAPIController : ControllerBase
	{
		private readonly QlbanQuanAoContext _context;
		private readonly JwtService _jwtService;
		private readonly IWebHostEnvironment _environment;
		public MyAccountAPIController(QlbanQuanAoContext context, IWebHostEnvironment environment, JwtService jwtService)
		{
			_context = context;
			_environment = environment;
			_jwtService = jwtService;
		}

		#region Địa chỉ giao hàng

		[HttpPatch("EditAddress")]
		public IActionResult EditAddress([FromBody] EditAddress model)
		{
			if (model == null || model.Id <= 0)
				return BadRequest("Invalid address data");
			if (!ModelState.IsValid)
				return BadRequest(ModelState);

			// Tìm địa chỉ giao hàng dựa trên Id
			var userAddress = _context.ShippingAddresses.FirstOrDefault(a => a.Id == model.Id);
			if (userAddress != null)
			{
				userAddress.Fullname = model.Fullname ?? userAddress.Fullname;
				userAddress.PhoneNumber = model.PhoneNumber ?? userAddress.PhoneNumber;
				userAddress.Address = model.Address ?? userAddress.Address;

				// Cập nhật thông tin trong các đơn hàng sử dụng địa chỉ này (nếu cần)
				var ordersToUpdate = _context.Orders
					.Where(o => o.Id == model.Id && o.Active==true)
					.ToList();

				foreach (var order in ordersToUpdate)
				{
					order.Fullname = model.Fullname ?? order.Fullname;
					order.PhoneNumber = model.PhoneNumber ?? order.PhoneNumber;
					order.Address = model.Address ?? order.Address;
				}

				_context.SaveChanges();
				return Ok(new { success = true, message = "Address updated successfully" });
			}

			return NotFound(new { success = false, message = "Address not found" });
		}

		[HttpPatch("SetDefaultAddress")]
		public IActionResult SetDefaultAddress([FromBody] EditAddress model)
		{
			if (model == null || model.Id <= 0)
				return BadRequest("Invalid address data");

			var addressToSetDefault = _context.ShippingAddresses.FirstOrDefault(a => a.Id == model.Id);
			if (addressToSetDefault != null)
			{
				// Đặt tất cả địa chỉ của người dùng về không mặc định
				var userAddresses = _context.ShippingAddresses
					.Where(a => a.UserId == addressToSetDefault.UserId)
					.ToList();

				foreach (var addr in userAddresses)
				{
					addr.IsDefault = false;
				}

				// Đặt địa chỉ hiện tại thành mặc định
				addressToSetDefault.IsDefault = true;
				_context.SaveChanges();

				return Ok(new { success = true, message = "Address set as default successfully" });
			}

			return NotFound(new { success = false, message = "Address not found" });
		}

		[HttpDelete("DeleteAddress")]
		public IActionResult DeleteAddress([FromBody] EditAddress model)
		{
			if (model == null || model.Id <= 0)
				return BadRequest("Invalid address data");

			var addressToDelete = _context.ShippingAddresses.FirstOrDefault(a => a.Id == model.Id);
			if (addressToDelete != null)
			{
				_context.ShippingAddresses.Remove(addressToDelete);
				_context.SaveChanges();
				return Ok(new { success = true, message = "Address deleted successfully" });
			}

			return NotFound(new { success = false, message = "Address not found" });
		}

		[HttpPost("AddNewAddress")]
		public IActionResult AddNewAddress([FromBody] EditAddress model)
		{
			var customerId = GetCurrentCustomerId();
			if (customerId <= 0)
				return Unauthorized(new { success = false, message = "Unauthorized" });

			if (model == null)
				return BadRequest("Invalid address data");
			if (!ModelState.IsValid)
				return BadRequest(ModelState);

			var newAddress = new ShippingAddress
			{
				UserId = customerId,
				Fullname = model.Fullname,
				PhoneNumber = model.PhoneNumber,
				Address = model.Address,
				IsDefault = false
			};

			_context.ShippingAddresses.Add(newAddress);
			_context.SaveChanges();
			return Ok(new { success = true, message = "New address added successfully" });
		}
		[HttpPatch("ChangeOrderAddress")]
		public IActionResult ChangeOrderAddress([FromBody] ChangeOrderAddressModel model)
		{
			if (model == null || model.OrderId <= 0 || model.ShippingAddressId <= 0)
				return BadRequest("Invalid data");

			// Tìm đơn hàng theo OrderId
			var order = _context.Orders.FirstOrDefault(o => o.Id == model.OrderId);
			if (order == null)
				return NotFound(new { success = false, message = "Order not found" });

			// Kiểm tra trạng thái hiện tại của đơn hàng. Chỉ cho phép thay đổi nếu status là "processing"
			if (order.Status != "processing")
			{
				return BadRequest(new { success = false, message = "Order status does not allow address change" });
			}

			// Tìm địa chỉ giao hàng theo ShippingAddressId và đảm bảo thuộc về người dùng của đơn hàng
			var shippingAddress = _context.ShippingAddresses
				.FirstOrDefault(a => a.Id == model.ShippingAddressId && a.UserId == order.UserId);
			if (shippingAddress == null)
				return NotFound(new { success = false, message = "Shipping address not found" });

			// Cập nhật thông tin địa chỉ giao hàng cho đơn hàng
			order.Fullname = shippingAddress.Fullname;
			order.PhoneNumber = shippingAddress.PhoneNumber;
			order.Address = shippingAddress.Address;

			// Cập nhật trạng thái mới để đánh dấu đã thay đổi địa chỉ
			order.Status = "addressChanged";

			_context.SaveChanges();
			return Ok(new { success = true, message = "Order shipping address updated successfully" });
		}
		[HttpPatch("CancelOrder")]
        public async Task<IActionResult> CancelOrder([FromBody] CancelOrderModel model)
        {
            try
            {
                if (model == null || model.OrderId <= 0)
                    return BadRequest(new { success = false, message = "Invalid order data" });

                var customerId = GetCurrentCustomerId();
                if (customerId <= 0)
                    return Unauthorized(new { success = false, message = "Unauthorized" });

                // Tìm đơn hàng theo OrderId và bao gồm chi tiết đơn hàng với thông tin biến thể sản phẩm
                var order = await _context.Orders
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.ProductVariant)
                    .FirstOrDefaultAsync(o => o.Id == model.OrderId && o.UserId == customerId);

                if (order == null)
                    return NotFound(new { success = false, message = "Order not found" });

                // Kiểm tra trạng thái hiện tại của đơn hàng
                if (order.Status != "processing" && order.Status != "addressChanged")
                    return BadRequest(new { success = false, message = "Order status does not allow cancellation" });

                // DEBUG: Log thông tin đơn hàng và chi tiết
                Console.WriteLine($"=== CANCELLING ORDER DEBUG ===");
                Console.WriteLine($"Order ID: {order.Id}, Status: {order.Status}");
                Console.WriteLine($"Found {order.OrderDetails?.Count ?? 0} order details");

                // Restore stock quantities for all order items - HOÀN TRẢ SỐ LƯỢNG VÀO KHO
                if (order.OrderDetails != null)
                {
                    foreach (var orderDetail in order.OrderDetails)
                    {
                        if (orderDetail.ProductVariantId.HasValue && orderDetail.NumberOfProducts.HasValue)
                        {
                            var productVariant = await _context.ProductVariants
                                .FirstOrDefaultAsync(pv => pv.Id == orderDetail.ProductVariantId.Value);

                            if (productVariant != null)
                            {
                                // Lưu lại số lượng cũ để log
                                int oldStock = productVariant.StockQuantity;

                                // Thêm số lượng trở lại kho
                                productVariant.StockQuantity += orderDetail.NumberOfProducts.Value;

                                // Log thông tin hoàn trả
                                Console.WriteLine($"Restored {orderDetail.NumberOfProducts.Value} units for product variant {productVariant.Id}");
                                Console.WriteLine($"Product: {productVariant.ProductId}, Size: {productVariant.Size}, Color: {productVariant.Color}");
                                Console.WriteLine($"Stock changed from {oldStock} to {productVariant.StockQuantity}");
                            }
                            else
                            {
                                Console.WriteLine($"Product variant not found for ID: {orderDetail.ProductVariantId}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Invalid order detail - VariantId: {orderDetail.ProductVariantId}, Quantity: {orderDetail.NumberOfProducts}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No order details found for this order");
                }

                Console.WriteLine($"=== END DEBUG ===");

                // Cập nhật trạng thái thành cancelled
                order.Status = "cancelled";
                order.Active = false;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Order canceled successfully and product quantities have been restored to stock"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cancelling order: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error cancelling order",
                    error = ex.Message
                });
            }
        }

        [HttpPost("AddReview")]
        public async Task<IActionResult> AddReview([FromForm] ReviewCreateModel model)
        {
            if (model == null)
                return BadRequest("Dữ liệu review không hợp lệ");

            int userId = GetCurrentCustomerId();
            if (userId <= 0)
                return Unauthorized("Người dùng không hợp lệ");

            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == model.ProductId);
            if (existingReview != null)
            {
                return BadRequest("Bạn đã đánh giá sản phẩm này rồi.");
            }

            var review = new Review
            {
                UserId = userId,
                ProductId = model.ProductId,
                Rating = (byte)model.Rating,
                ReviewText = model.ReviewText,
                ReviewDate = DateTime.Now
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync(); // Lưu review trước để có review.ReviewId

            // --- file config: use consistent relative folder
            string relativeFolder = "/uploads/image/";
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "image");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (model.MediaFiles != null && model.MediaFiles.Any())
            {
                foreach (var file in model.MediaFiles)
                {
                    if (!new[] { "image/jpeg", "image/png", "video/mp4", "video/webm" }
                            .Contains(file.ContentType))
                    {
                        continue;
                    }

                    if (file.Length > 15 * 1024 * 1024)
                    {
                        continue;
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var reviewMedia = new ReviewMedium
                    {
                        ReviewId = review.ReviewId,
                        MediaType = file.ContentType,
                        MediaUrl = relativeFolder + uniqueFileName
                    };

                    _context.ReviewMedia.Add(reviewMedia);
                }

                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "Review và file media đã được lưu thành công" });
        }
        [HttpGet("GetReviewsByProduct/{productId}")]
		public async Task<IActionResult> GetReviewsByProduct(int productId)
		{
			if (productId <= 0)
				return BadRequest("Invalid product id.");

			var reviews = await _context.Reviews
				.Where(r => r.ProductId == productId)
				.Include(r => r.User)         // Lấy thông tin người dùng (nếu có)
				.Include(r => r.ReviewMedia)  // Lấy các file media kèm theo review (nếu có)
				.OrderByDescending(r => r.ReviewDate)
				.ToListAsync();

			var result = reviews.Select(r => new
			{
				ReviewId = r.ReviewId,
				UserName = r.User != null ? r.User.Fullname : "Anonymous",
				ReviewDate = r.ReviewDate,
				Rating = r.Rating,
				UserId = r.UserId,
				ReviewText = r.ReviewText,
				Media = r.ReviewMedia.Select(m => new
				{
					m.MediaType,
					m.MediaUrl
				})
			});

			return Ok(result);
		}
		[HttpGet("HasReviewed/{productId}")]
		public async Task<IActionResult> HasReviewed(int productId)
		{
			if (productId <= 0)
				return BadRequest("Invalid product id.");

			int currentUserId = GetCurrentCustomerId();
			if (currentUserId == -1)
				return Unauthorized("User is not authenticated.");

			bool reviewed = await _context.Reviews
				.AnyAsync(r => r.ProductId == productId && r.UserId == currentUserId);

			return Ok(new { reviewed });
		}

        [HttpPut("UpdateReview")]
        public async Task<IActionResult> UpdateReview([FromForm] ReviewCreateModel model)
        {
            if (model == null)
                return BadRequest("Dữ liệu review không hợp lệ");

            int currentUserId = GetCurrentCustomerId();
            if (currentUserId <= 0)
                return Unauthorized("Người dùng không hợp lệ");

            var existingReview = await _context.Reviews
                .Include(r => r.ReviewMedia)
                .FirstOrDefaultAsync(r => r.ProductId == model.ProductId && r.UserId == currentUserId);

            if (existingReview == null)
                return NotFound("Review không tồn tại");

            existingReview.Rating = (byte)model.Rating;
            existingReview.ReviewText = model.ReviewText;
            existingReview.ReviewDate = DateTime.Now;

            // file folders + relative url
            string relativeFolder = "/uploads/image/";
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "image");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (model.MediaFiles != null && model.MediaFiles.Any())
            {
                // --- Xóa file cũ trên disk (nếu có)
                foreach (var old in existingReview.ReviewMedia.ToList())
                {
                    try
                    {
                        var trimmed = old.MediaUrl?.TrimStart('/');
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            var pathOnDisk = Path.Combine(_environment.WebRootPath, trimmed.Replace('/', Path.DirectorySeparatorChar));
                            if (System.IO.File.Exists(pathOnDisk))
                            {
                                System.IO.File.Delete(pathOnDisk);
                            }
                        }
                    }
                    catch
                    {
                        // log nếu cần, nhưng không ngăn quá trình lưu file mới
                    }
                }

                // Xóa record media cũ khỏi DB
                _context.ReviewMedia.RemoveRange(existingReview.ReviewMedia);
                existingReview.ReviewMedia.Clear();

                // Lưu file mới và add vào DB
                foreach (var file in model.MediaFiles)
                {
                    if (!new[] { "image/jpeg", "image/png", "video/mp4", "video/webm" }
                            .Contains(file.ContentType))
                    {
                        continue;
                    }
                    if (file.Length > 50 * 1024 * 1024)
                    {
                        continue;
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var reviewMedia = new ReviewMedium
                    {
                        ReviewId = existingReview.ReviewId,
                        MediaType = file.ContentType,
                        MediaUrl = relativeFolder + uniqueFileName
                    };

                    _context.ReviewMedia.Add(reviewMedia);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Review đã được cập nhật thành công." });
        }


        //[HttpPut("UpdateReview")]
        //public async Task<IActionResult> UpdateReview([FromForm] ReviewCreateModel model)
        //{
        //    if (model == null)
        //        return BadRequest("Dữ liệu review không hợp lệ");

        //    int currentUserId = GetCurrentCustomerId();
        //    if (currentUserId <= 0)
        //        return Unauthorized("Người dùng không hợp lệ");

        //    var existingReview = await _context.Reviews
        //        .Include(r => r.ReviewMedia)
        //        .FirstOrDefaultAsync(r => r.ProductId == model.ProductId && r.UserId == currentUserId);

        //    if (existingReview == null)
        //        return NotFound("Review không tồn tại");

        //    existingReview.Rating = (byte)model.Rating;
        //    existingReview.ReviewText = model.ReviewText;
        //    existingReview.ReviewDate = DateTime.Now;

        //    // file folders + relative url
        //    string relativeFolder = "/uploads/image/";
        //    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "image");
        //    if (!Directory.Exists(uploadsFolder))
        //    {
        //        Directory.CreateDirectory(uploadsFolder);
        //    }

        //    if (model.MediaFiles != null && model.MediaFiles.Any())
        //    {
        //        // Nếu bạn muốn giới hạn tổng file cho mỗi review, bật biến này:
        //        int maxFilesPerReview = 10; // ví dụ: tối đa 10 file cho mỗi review
        //                                    // currentCount = số media đang có; nếu không muốn giới hạn, set maxFilesPerReview = int.MaxValue
        //        int currentCount = existingReview.ReviewMedia?.Count ?? 0;

        //        foreach (var file in model.MediaFiles)
        //        {
        //            // Nếu đã đạt limit, bỏ qua các file sau
        //            if (currentCount >= maxFilesPerReview)
        //            {
        //                break;
        //            }

        //            // (Tùy chọn) Kiểm tra định dạng và kích thước file
        //            var allowed = new[] { "image/jpeg", "image/png", "video/mp4", "video/webm" };
        //            long maxPerFile = 100L * 1024 * 1024; // 100MB per file (tùy chỉnh)

        //            if (!allowed.Contains(file.ContentType))
        //            {
        //                // bạn có thể return BadRequest ở đây nếu muốn cho user biết file không hợp lệ
        //                continue;
        //            }
        //            if (file.Length > maxPerFile)
        //            {
        //                // tiếp tục hoặc trả lỗi tuỳ bạn
        //                continue;
        //            }

        //            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
        //            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

        //            using (var stream = new FileStream(filePath, FileMode.Create))
        //            {
        //                await file.CopyToAsync(stream);
        //            }

        //            var reviewMedia = new ReviewMedium
        //            {
        //                ReviewId = existingReview.ReviewId,
        //                MediaType = file.ContentType,
        //                MediaUrl = relativeFolder + uniqueFileName
        //            };

        //            // Thêm trực tiếp vào bảng ReviewMedia để EF tracking tốt
        //            _context.ReviewMedia.Add(reviewMedia);

        //            currentCount++; // tăng số file đã thêm
        //        }
        //    }

        //    await _context.SaveChangesAsync();

        //    return Ok(new { success = true, message = "Review đã được cập nhật thành công." });
        //}

        #endregion

        #region Thông tin tài khoản

        [HttpGet("MyAccount")]
		public IActionResult MyAccount()
		{
			var customerId = GetCurrentCustomerId();
			if (customerId <= 0)
				return Unauthorized(new { success = false, message = "Unauthorized" });

			// Nếu người dùng không có đơn hàng nào đã hoàn thành (Active == false)
			if (!_context.Orders.Any(o => o.UserId == customerId && o.Active == false))
			{
				var account = (from d in _context.Users
							   where d.Id == customerId && d.IsActive == true
							   select new Account
							   {
								   Id = d.Id,
								   Fullname = d.Fullname,
								   PhoneNumber = d.PhoneNumber
							   }).ToList();

				return Ok(new { success = true, data = account });
			}

            // Nếu có đơn hàng hoàn thành, lấy thông tin chi tiết đơn hàng cùng variant (nếu có)
            var accountItems = (from a in _context.Products
                                join b in _context.OrderDetails on a.Id equals b.ProductId
                                join c in _context.Orders on b.OrderId equals c.Id
                                join d in _context.Users on c.UserId equals d.Id
                                // left join vào ProductVariants
                                join pv in _context.ProductVariants on b.ProductVariantId equals pv.Id into pvGroup
                                from pv in pvGroup.DefaultIfEmpty()
                                where c.Active == false
                                   && c.UserId == customerId
                                   && d.IsActive == true
                                select new Account
                                {
                                    Name = a.Name,
                                    ProductId = a.Id,
                                    Id = d.Id,
                                    OrderId = c.Id,
                                    Fullname = d.Fullname,
                                    FullnameS = c.Fullname,
                                    PaymentMethod = c.PaymentMethod,
                                    CaptureId = c.CaptureId,
                                    TotalMoney = c.TotalMoney,
                                    PhoneNumberS = c.PhoneNumber,
                                    AddressS = c.Address,
                                    ProductImageUrl = a.Image,
                                    Price = a.Price,
                                    Status = c.Status,
                                    NumberOfProducts = b.NumberOfProducts,
                                    PhoneNumber = d.PhoneNumber,
                                    OrderDate = c.OrderDate,
                                    // variant info — nếu pv null thì trả null (PASCALCASE names)
                                    ProductVariantId = b.ProductVariantId,
                                    Size = pv != null ? pv.Size : null,
                                    Color = pv != null ? pv.Color : null
                                }).ToList();

            return Ok(new { success = true, data = accountItems });
		}
		[HttpGet("GetShippingAddresses")]
		public IActionResult GetShippingAddresses()
		{
			var customerId = GetCurrentCustomerId();
			if (customerId <= 0)
			{
				return Unauthorized(new { success = false, message = "Unauthorized" });
			}

			var addresses = _context.ShippingAddresses
				.Where(o => o.UserId == customerId)
				.ToList();

			return Ok(new { success = true, data = addresses });
		}

		#endregion

		#region Hỗ trợ

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

		#endregion
	}
}
