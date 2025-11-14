using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using test1.Handlers;
using test1.Models;
using test1.Models.AccessModel;
using test1.Models.Authentication;
using test1.Service;

namespace test1.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class AccountAPIController : ControllerBase
	{
		private readonly JwtService _jwtService;
		private readonly QlbanQuanAoContext _context;
		private readonly IMemoryCache _memoryCache; // Inject IMemoryCache
		private readonly TwilioService _twilioService; // Inject TwilioService

		public AccountAPIController(
			JwtService jwtService,
			QlbanQuanAoContext context,
			IMemoryCache memoryCache,
			TwilioService twilioService)
		{
			_jwtService = jwtService;
			_context = context;
			_memoryCache = memoryCache;
			_twilioService = twilioService;
		}

		// Đăng nhập
		[AllowAnonymous]
		[HttpPost("Login")]
		public async Task<ActionResult> Login([FromBody] LoginRequestModel request)
		{
			// Kiểm tra nếu đã có accessToken trong cookie thì thu hồi và xóa cookie đó
			var existingToken = Request.Cookies["accessToken"];
			if (!string.IsNullOrEmpty(existingToken))
			{
				var userData = _jwtService.GetUserDataFromToken(existingToken);
				if (userData != null)
				{
					await _jwtService.RevokeAllTokens(userData.PhoneNumber);
				}
				Response.Cookies.Delete("accessToken");
			}

			var result = await _jwtService.Authenticate(request);
			if (result == null)
			{
				return Unauthorized(new { Message = "Invalid credentials" });
			}

			// Lưu token vào cookie với tùy chọn HttpOnly
			var cookieOptions = new CookieOptions
			{
				HttpOnly = false,                      // Không cho phép truy cập từ JavaScript
				Secure = false,                        // Chỉ gửi qua HTTPS
				//SameSite = SameSiteMode.Strict,       // Giới hạn cookie chỉ gửi từ cùng một site
				Expires = DateTime.UtcNow.AddSeconds(result.ExpiresIn)
			};

			Response.Cookies.Append("accessToken", result.AccessToken, cookieOptions);

			return Ok(new { Message = "Login successful", Token = result.AccessToken });

		}

        // Endpoint đăng ký, chỉ gửi OTP và lưu thông tin đăng ký tạm thời
        [AllowAnonymous]
        [HttpPost("Register")]
        public async Task<ActionResult> Register([FromBody] RegisterRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { Message = "Số điện thoại và mật khẩu là bắt buộc" });

            var normalizedPhoneNumber = NormalizePhoneNumber(request.PhoneNumber);

            // Lưu tạm thông tin đăng ký (không chứa OTP)
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(10)); // sống lâu hơn mã OTP một chút
            _memoryCache.Set($"Registration_{normalizedPhoneNumber}", request, cacheEntryOptions);

            // Gửi OTP qua Twilio Verify
            var status = await _twilioService.StartVerificationAsync(normalizedPhoneNumber, "sms");
            if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                return StatusCode(StatusCodes.Status500InternalServerError, new { Message = "Không thể gửi OTP" });

            return Ok(new { Message = "OTP đã được gửi, vui lòng xác thực" });
        }

        [AllowAnonymous]
        [HttpPost("VerifyOTP")]
        public async Task<ActionResult> VerifyOTP([FromBody] OTPVerificationRequest request)
        {
            var normalizedPhoneNumber = NormalizePhoneNumber(request.PhoneNumber);

            // Kiểm tra mã OTP với Twilio Verify
            var approved = await _twilioService.CheckVerificationAsync(normalizedPhoneNumber, request.OTP);
            if (!approved)
                return BadRequest(new { Message = "OTP không hợp lệ hoặc đã hết hạn" });

            // Lấy thông tin đăng ký tạm
            if (!_memoryCache.TryGetValue($"Registration_{normalizedPhoneNumber}", out RegisterRequestModel registerRequest))
                return BadRequest(new { Message = "Thông tin đăng ký không tồn tại hoặc đã hết hạn" });

            // Tạo tài khoản
            var result = await _jwtService.Register(registerRequest);
            if (result == null)
                return BadRequest(new { Message = "Đăng ký không thành công" });

            // Xóa cache tạm
            _memoryCache.Remove($"Registration_{normalizedPhoneNumber}");

            return Ok(new { Message = "Xác thực OTP thành công, tài khoản đã được tạo" });
        }

        private string NormalizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            phoneNumber = phoneNumber.Trim();

            // Nếu đã bắt đầu bằng +, giả sử người dùng nhập đúng chuẩn E.164
            if (phoneNumber.StartsWith("+"))
                return phoneNumber;

            // Nếu bắt đầu bằng "0", chuyển thành +84 (Việt Nam)
            if (phoneNumber.StartsWith("0"))
            {
                // Ví dụ: 0936... => +84936...
                return "+84" + phoneNumber.Substring(1);
            }

            // Tuỳ theo nhu cầu, có thể thêm logic cho các đầu số khác,
            // nhưng ở đây giả sử người dùng chủ yếu là ở Việt Nam.
            // Nếu không rơi vào trường hợp trên, ta cứ trả về phoneNumber như đã nhập.
            return phoneNumber;
        }
        // Đăng xuất
        [HttpPost("Logout")]
		public async Task<IActionResult> Logout()
		{
			var accessToken = Request.Cookies["accessToken"];
			if (string.IsNullOrEmpty(accessToken))
				return Unauthorized(new { Message = "No access token found" });

			var userData = _jwtService.GetUserDataFromToken(accessToken);
			if (userData == null)
				return Unauthorized(new { Message = "Invalid token" });

			await _jwtService.RevokeAllTokens(userData.PhoneNumber);
			Response.Cookies.Delete("accessToken");

			return Ok(new { Message = "Logout successful" });
		}

		// Thay đổi mật khẩu
		[HttpPost("ChangePassword")]
		public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
		{
			// Cố gắng lấy token từ cookie
			var accessToken = Request.Cookies["accessToken"];

			// Nếu không có token trong cookie, thử lấy từ header
			if (string.IsNullOrEmpty(accessToken))
			{
				var authHeader = Request.Headers["Authorization"].ToString();
				if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
				{
					accessToken = authHeader.Substring("Bearer ".Length).Trim();
				}
			}
			var userData = _jwtService.GetUserDataFromToken(accessToken);
			if (userData == null)
				return Unauthorized(new { Message = "Invalid token" });

			var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == userData.PhoneNumber);
			if (user == null || !PasswordHashHandler.VerifyPassword(request.OldPassword, user.Password))
				return BadRequest(new { Message = "Incorrect current password" });

			// Cập nhật mật khẩu mới
			user.Password = PasswordHashHandler.HashPassword(request.NewPassword);
			await _context.SaveChangesAsync();

			// Thu hồi token cũ và xóa cookie
			await _jwtService.RevokeAllTokens(user.PhoneNumber);
			Response.Cookies.Delete("accessToken");

			return Ok(new { Message = "Password changed successfully" });
		}

		// Cập nhật thông tin tài khoản
		[HttpPost("UpdateAccount")]
		public async Task<ActionResult> UpdateAccount([FromBody] UpdateAccountRequest request)
		{
			// Cố gắng lấy token từ cookie
			var accessToken = Request.Cookies["accessToken"];

			// Nếu không có token trong cookie, thử lấy từ header
			if (string.IsNullOrEmpty(accessToken))
			{
				var authHeader = Request.Headers["Authorization"].ToString();
				if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
				{
					accessToken = authHeader.Substring("Bearer ".Length).Trim();
				}
			}
			var userData = _jwtService.GetUserDataFromToken(accessToken);
			if (userData == null)
				return Unauthorized(new { Message = "Invalid token" });

			var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == userData.PhoneNumber);
			if (user == null)
				return NotFound(new { Message = "User not found" });

			user.Fullname = request.Fullname;
			user.PhoneNumber = request.PhoneNumber;
			user.UpdatedAt = DateTime.Now;
			await _context.SaveChangesAsync();

			return Ok(new { Message = "Account details updated successfully" });
		}

		/*
        // Ví dụ về refresh token (nếu cần sử dụng)
        [AllowAnonymous]
        [HttpPost("RefreshToken")]
        public async Task<ActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _jwtService.RefreshToken(request.RefreshToken);
            if (result == null)
            {
                return Unauthorized(new { Message = "Invalid or expired refresh token" });
            }

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddSeconds(result.ExpiresIn)
            };

            Response.Cookies.Append("accessToken", result.AccessToken, cookieOptions);

            return Ok(new { Message = "Token refreshed successfully", AccessToken = result.AccessToken });
        }
        */
	}
}
