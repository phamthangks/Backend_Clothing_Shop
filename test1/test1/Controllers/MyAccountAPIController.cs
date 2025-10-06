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
		public IActionResult CancelOrder([FromBody] CancelOrderModel model)
		{
			if (model == null || model.OrderId <= 0)
				return BadRequest("Invalid order data");

			// Tìm đơn hàng theo OrderId
			var order = _context.Orders.FirstOrDefault(o => o.Id == model.OrderId);
			if (order == null)
				return NotFound(new { success = false, message = "Order not found" });

			// Kiểm tra trạng thái hiện tại của đơn hàng
			if (order.Status != "processing" && order.Status != "addressChanged")
				return BadRequest(new { success = false, message = "Order status does not allow cancellation" });

			// Cập nhật trạng thái thành cancel
			order.Status = "cancelled";
			_context.SaveChanges();
			return Ok(new { success = true, message = "Order canceled successfully" });
		}
		[HttpPost("AddReview")]
		public async Task<IActionResult> AddReview([FromForm] ReviewCreateModel model)
		{
			if (model == null)
				return BadRequest("Dữ liệu review không hợp lệ");

			// Lấy id người dùng từ token (tương tự như hàm GetCurrentCustomerId trong MyAccountAPIController)
			int userId = GetCurrentCustomerId();
			if (userId <= 0)
				return Unauthorized("Người dùng không hợp lệ");
			// Kiểm tra xem người dùng đã đánh giá sản phẩm này chưa
			var existingReview = await _context.Reviews
				.FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == model.ProductId);
			if (existingReview != null)
			{
				return BadRequest("Bạn đã đánh giá sản phẩm này rồi.");
			}
			// Tạo đối tượng Review với dữ liệu nhận được
			var review = new Review
			{
				UserId = userId,
				ProductId = model.ProductId,
				Rating = (byte)model.Rating,
				ReviewText = model.ReviewText,
				ReviewDate = DateTime.Now
			};

			_context.Reviews.Add(review);
			await _context.SaveChangesAsync(); // Lưu review để có review_id (primary key)

			// Nếu có file media được đính kèm
			if (model.MediaFiles != null && model.MediaFiles.Any())
			{
				// Thư mục lưu file (ví dụ: wwwroot/uploads)
				string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
				if (!Directory.Exists(uploadsFolder))
				{
					Directory.CreateDirectory(uploadsFolder);
				}

				foreach (var file in model.MediaFiles)
				{
					// (Tùy chọn) Kiểm tra định dạng và kích thước file nếu cần
					// Ví dụ: chỉ cho phép image/jpeg, image/png, video/mp4, video/webm và kích thước tối đa 5MB

					if (!new[] { "image/jpeg", "image/png", "video/mp4", "video/webm" }
							.Contains(file.ContentType))
					{
						// Nếu không hợp lệ, bạn có thể bỏ qua file này hoặc trả về lỗi
						continue;
					}

					if (file.Length > 15 * 1024 * 1024)
					{
						continue;
					}

					// Tạo tên file duy nhất để tránh trùng lặp
					string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
					string filePath = Path.Combine(uploadsFolder, uniqueFileName);

					using (var stream = new FileStream(filePath, FileMode.Create))
					{
						await file.CopyToAsync(stream);
					}

					// Sau khi lưu file, tạo một đối tượng ReviewMedia với đường dẫn file
					var reviewMedia = new ReviewMedium
					{
						ReviewId = review.ReviewId,
						MediaType = file.ContentType,
						MediaUrl = "/uploads/" + uniqueFileName  // Đường dẫn URL tương đối, bạn có thể điều chỉnh nếu cần
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
		//[HttpPatch("ChangeOrderAddress")]
		//public IActionResult ChangeOrderAddress([FromBody] ChangeOrderAddressModel model)
		//{
		//	if (model == null || model.OrderId <= 0 || model.ShippingAddressId <= 0)
		//		return BadRequest("Invalid data");

		//	// Tìm đơn hàng đang xử lý (active == true) theo OrderId
		//	var order = _context.Orders.FirstOrDefault(o => o.Id == model.OrderId);
		//	if (order == null)
		//		return NotFound(new { success = false, message = "Order not found" });

		//	// Tìm địa chỉ giao hàng theo ShippingAddressId và đảm bảo địa chỉ thuộc về người dùng của đơn hàng
		//	var shippingAddress = _context.ShippingAddresses
		//		.FirstOrDefault(a => a.Id == model.ShippingAddressId && a.UserId == order.UserId);
		//	if (shippingAddress == null)
		//		return NotFound(new { success = false, message = "Shipping address not found" });

		//	// Cập nhật thông tin địa chỉ giao hàng cho đơn hàng
		//	order.Fullname = shippingAddress.Fullname;
		//	order.PhoneNumber = shippingAddress.PhoneNumber;
		//	order.Address = shippingAddress.Address;

		//	_context.SaveChanges();
		//	return Ok(new { success = true, message = "Order shipping address updated successfully" });
		//}
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

			// Lấy id người dùng từ token
			int currentUserId = GetCurrentCustomerId();
			if (currentUserId <= 0)
				return Unauthorized("Người dùng không hợp lệ");

			// Tìm review của người dùng cho sản phẩm đó
			var existingReview = await _context.Reviews
				.Include(r => r.ReviewMedia)
				.FirstOrDefaultAsync(r => r.ProductId == model.ProductId && r.UserId == currentUserId);

			if (existingReview == null)
				return NotFound("Review không tồn tại");

			// Cập nhật thông tin review
			existingReview.Rating = (byte)model.Rating;
			existingReview.ReviewText = model.ReviewText;
			existingReview.ReviewDate = DateTime.Now; // cập nhật lại thời gian chỉnh sửa

			// Nếu có file media mới được gửi lên, cập nhật lại media
			if (model.MediaFiles != null && model.MediaFiles.Any())
			{
				// Xóa media cũ
				_context.ReviewMedia.RemoveRange(existingReview.ReviewMedia);
				existingReview.ReviewMedia = new List<ReviewMedium>();

				// Thư mục lưu file (ví dụ: wwwroot/uploads)
				string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
				if (!Directory.Exists(uploadsFolder))
				{
					Directory.CreateDirectory(uploadsFolder);
				}

				foreach (var file in model.MediaFiles)
				{
					// (Tùy chọn) Kiểm tra định dạng và kích thước file
					if (!new[] { "image/jpeg", "image/png", "video/mp4", "video/webm" }
							.Contains(file.ContentType))
					{
						continue;
					}
					if (file.Length > 15 * 1024 * 1024)
					{
						continue;
					}

					// Tạo tên file duy nhất
					string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
					string filePath = Path.Combine(uploadsFolder, uniqueFileName);

					using (var stream = new FileStream(filePath, FileMode.Create))
					{
						await file.CopyToAsync(stream);
					}

					// Tạo đối tượng ReviewMedia mới với đường dẫn file
					var reviewMedia = new ReviewMedium
					{
						ReviewId = existingReview.ReviewId,
						MediaType = file.ContentType,
						MediaUrl = "/uploads/" + uniqueFileName
					};

					existingReview.ReviewMedia.Add(reviewMedia);
				}
			}

			await _context.SaveChangesAsync();

			return Ok(new { success = true, message = "Review đã được cập nhật thành công." });
		}

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

			// Nếu có đơn hàng hoàn thành, lấy thông tin chi tiết đơn hàng
			var accountItems = (from a in _context.Products
								join b in _context.OrderDetails on a.Id equals b.ProductId
								join c in _context.Orders on b.OrderId equals c.Id
								join d in _context.Users on c.UserId equals d.Id
								where c.Active == false
								   && c.UserId == customerId
								   && d.IsActive == true
								select new Account
								{
									Name = a.Name,
									ProductId = a.Id,
									Id = d.Id,
									OrderId= c.Id,
									Fullname = d.Fullname,
									FullnameS=c.Fullname,
									PaymentMethod=c.PaymentMethod,
									CaptureId=c.CaptureId,
									TotalMoney=c.TotalMoney,
									PhoneNumberS=c.PhoneNumber,
									AddressS=c.Address,
									ProductImageUrl=a.Image,
									Price = a.Price,
									Status = c.Status,
									NumberOfProducts = b.NumberOfProducts,
									PhoneNumber = d.PhoneNumber,
									OrderDate = c.OrderDate,
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
