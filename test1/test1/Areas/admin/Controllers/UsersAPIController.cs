using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test1.Models;
using test1.Models.AccessModel;
using test1.Service;

namespace test1.Areas.Admin.Controllers
{
	[Area("admin")]
	[Route("api/users")]
	[ApiController]
	public class UsersAPIController : Controller
	{
		private readonly QlbanQuanAoContext _context = new QlbanQuanAoContext();
		private readonly JwtService _jwtService;

		public UsersAPIController(JwtService jwtService, QlbanQuanAoContext context)
		{
			_jwtService = jwtService;
			_context = context;
		}
		[HttpGet]
		public IActionResult GetAll(int page = 1, int size = 10)
		{
			var users = _context.Users
				.Skip((page - 1) * size)
				.Take(size)
				.ToList();
			var total = _context.Users.Count();

			return Ok(new
			{
				data = users,
				total = total,
				page = page,
				size = size
			});
		}

		[HttpPost("Create")]
		public IActionResult Create([FromBody] User user)
		{
			if (ModelState.IsValid)
			{
				_context.Users.Add(user);
				_context.SaveChanges();
				return Ok(new { message = "Người dùng đã được tạo thành công", userId = user.Id });
			}
			return BadRequest(ModelState);
		}

		[HttpPut("Edit/{id}")]
		public IActionResult Edit(int id, [FromBody] User updatedUser)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			var user = _context.Users.Find(id);
			if (user == null)
			{
				return NotFound(new { message = "Không tìm thấy người dùng" });
			}

			// Cập nhật các trường thông tin
			user.Fullname = updatedUser.Fullname;
			user.PhoneNumber = updatedUser.PhoneNumber;
			user.Password = updatedUser.Password;
			user.DateOfBirth = updatedUser.DateOfBirth;
			user.IsActive = updatedUser.IsActive;
			user.FacebookAccountId = updatedUser.FacebookAccountId;
			user.GoogleAccountId = updatedUser.GoogleAccountId;
			user.RoleId = updatedUser.RoleId;
			user.UpdatedAt = DateTime.Now;

			_context.SaveChanges();

			return Ok(new { success = true, message = "Người dùng đã được cập nhật thành công!" });
		}


		[HttpDelete("Delete/{id}")]
		public IActionResult Delete(int id)
		{
			var order = _context.Orders.Where(o => o.UserId == id).ToList();
			_context.Orders.RemoveRange(order);

			var token = _context.Tokens.Where(o => o.UserId == id).ToList();
			_context.Tokens.RemoveRange(token);

			var sp = _context.ShippingAddresses.Where(o => o.UserId == id).ToList();
			_context.ShippingAddresses.RemoveRange(sp);

			var user = _context.Users.Find(id);
			if (user == null)
			{
				return NotFound(new { message = "Không tìm thấy người dùng" });
			}

			_context.Users.Remove(user);
			_context.SaveChanges();

			return Ok(new { message = "Người dùng đã được xóa thành công" });
		}
		// GET api/users/name
		[HttpGet("name")]
		public IActionResult GetMyFullName()
		{
			int myId = GetCurrentCustomerId();
			if (myId < 0)
				return Unauthorized(new { message = "Token không hợp lệ hoặc hết hạn." });

			var user = _context.Users.Find(myId);
			if (user == null)
				return NotFound(new { message = "Không tìm thấy người dùng." });

			return Ok(new { fullName = user.Fullname });
		}

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
