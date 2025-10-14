using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test1.Models;
using test1.Models.AccessModel;
using test1.Service;
using test1.Handlers;
using System.Text.RegularExpressions;
using System.Text.Json;

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
		public IActionResult GetAll(int page = 1, int size = 10, string? search = null, int? roleId = null, bool? isActive = null)
		{
			if (page <= 0) page = 1;
			if (size <= 0) size = 10;

			var query = _context.Users
				.Include(u => u.Role)
				.AsQueryable();

			if (!string.IsNullOrWhiteSpace(search))
			{
				var keyword = search.Trim().ToLower();
				query = query.Where(u =>
					(u.Fullname != null && EF.Functions.Like(u.Fullname.ToLower(), $"%{keyword}%")) ||
					EF.Functions.Like(u.PhoneNumber.ToLower(), $"%{keyword}%"));
			}

			if (roleId.HasValue)
			{
				query = query.Where(u => u.RoleId == roleId.Value);
			}

			if (isActive.HasValue)
			{
				query = query.Where(u => u.IsActive == isActive.Value);
			}

			var total = query.Count();
			var users = query
				.Skip((page - 1) * size)
				.Take(size)
				.Select(u => new
				{
					id = u.Id,
					fullname = u.Fullname,
					phoneNumber = u.PhoneNumber,
					isActive = u.IsActive,
					roleId = u.RoleId,
					role = u.Role == null ? null : new { id = u.Role.Id, name = u.Role.Name },
					facebookAccountId = u.FacebookAccountId,
					googleAccountId = u.GoogleAccountId,
					createdAt = u.CreatedAt,
					updatedAt = u.UpdatedAt
				})
				.ToList();

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
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			// Manual validations (since EF model lacks DataAnnotations)
			if (string.IsNullOrWhiteSpace(user.PhoneNumber))
			{
				return BadRequest(new { message = "Số điện thoại là bắt buộc" });
			}
			if (!Regex.IsMatch(user.PhoneNumber, "^[0-9]{10}$"))
			{
				return BadRequest(new { message = "Số điện thoại không hợp lệ (10 chữ số)" });
			}
			if (string.IsNullOrWhiteSpace(user.Password) || user.Password.Length < 6)
			{
				return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự" });
			}
			if (!user.RoleId.HasValue)
			{
				return BadRequest(new { message = "Role là bắt buộc" });
			}

			// Duplicate phone check
			bool phoneExists = _context.Users.Any(u => u.PhoneNumber == user.PhoneNumber);
			if (phoneExists)
			{
				return Conflict(new { message = "Số điện thoại đã tồn tại" });
			}

			try
			{
				// Hash password before saving
				user.Password = PasswordHashHandler.HashPassword(user.Password);
				// Ensure social IDs default to 0 if not provided
				user.FacebookAccountId = user.FacebookAccountId ?? 0;
				user.GoogleAccountId = user.GoogleAccountId ?? 0;
				user.CreatedAt = DateTime.Now;
				_context.Users.Add(user);
				_context.SaveChanges();
				return Ok(new { message = "Người dùng đã được tạo thành công", userId = user.Id });
			}
			catch (DbUpdateException ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi khi lưu dữ liệu", detail = ex.Message });
			}
		}

	[HttpPut("Edit/{id}")]
	public IActionResult Edit(int id, [FromBody] JsonElement updateData)
	{
		var user = _context.Users.Find(id);
		if (user == null)
		{
			return NotFound(new { message = "Không tìm thấy người dùng" });
		}

		// Extract fields from JsonElement
		string? phoneNumber = updateData.TryGetProperty("phoneNumber", out var phoneEl) ? phoneEl.GetString() : null;
		string? fullname = updateData.TryGetProperty("fullname", out var nameEl) ? nameEl.GetString() : null;
		int? roleId = updateData.TryGetProperty("roleId", out var roleEl) ? roleEl.GetInt32() : null;
		bool? isActive = updateData.TryGetProperty("isActive", out var activeEl) ? activeEl.GetBoolean() : null;
		string? password = updateData.TryGetProperty("password", out var passEl) ? passEl.GetString() : null;

		// Validate phone
		if (string.IsNullOrWhiteSpace(phoneNumber))
		{
			return BadRequest(new { message = "Số điện thoại là bắt buộc" });
		}
		if (!Regex.IsMatch(phoneNumber, "^[0-9]{10}$"))
		{
			return BadRequest(new { message = "Số điện thoại không hợp lệ (10 chữ số)" });
		}
		bool phoneExists = _context.Users.Any(u => u.PhoneNumber == phoneNumber && u.Id != id);
		if (phoneExists)
		{
			return Conflict(new { message = "Số điện thoại đã tồn tại" });
		}

		if (!roleId.HasValue)
		{
			return BadRequest(new { message = "Role là bắt buộc" });
		}

		// Cập nhật các trường thông tin
		user.Fullname = fullname;
		user.PhoneNumber = phoneNumber;
		// Only re-hash password if provided and changed
		if (!string.IsNullOrWhiteSpace(password))
		{
			// If password is different from current hash, re-hash it
			if (password != user.Password)
			{
				user.Password = PasswordHashHandler.HashPassword(password);
			}
		}
		// If password is empty/null, keep the existing password (don't update)
		user.IsActive = isActive ?? user.IsActive;
		// Không cho phép sửa Facebook/Google Account ID từ admin
		// Giữ nguyên các giá trị hiện tại
		user.FacebookAccountId = user.FacebookAccountId ?? 0;
		user.GoogleAccountId = user.GoogleAccountId ?? 0;
		user.RoleId = roleId;
		user.UpdatedAt = DateTime.Now;

			try
			{
				_context.SaveChanges();
			}
			catch (DbUpdateException ex)
			{
				// Unique index violation on phone number
				return Conflict(new { message = "Số điện thoại đã tồn tại", detail = ex.Message });
			}

			return Ok(new { success = true, message = "Người dùng đã được cập nhật thành công!" });
		}


		[HttpDelete("Delete/{id}")]
		public IActionResult Delete(int id)
		{
			var user = _context.Users.Find(id);
			if (user == null)
			{
				return NotFound(new { message = "Không tìm thấy người dùng" });
			}

			// Soft delete: set inactive instead of removing
			user.IsActive = false;
			user.UpdatedAt = DateTime.Now;
			_context.Users.Update(user);
			_context.SaveChanges();

			return Ok(new { message = "Tài khoản đã được vô hiệu hóa" });
		}

		[HttpPut("Restore/{id}")]
		public IActionResult Restore(int id)
		{
			var user = _context.Users.Find(id);
			if (user == null)
			{
				return NotFound(new { message = "Không tìm thấy người dùng" });
			}

			user.IsActive = true;
			user.UpdatedAt = DateTime.Now;
			_context.Users.Update(user);
			_context.SaveChanges();

			return Ok(new { message = "Tài khoản đã được khôi phục" });
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
