using Microsoft.AspNetCore.Mvc;
using test1.Models.Authentication;
using test1.Service;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
	private readonly JwtService _jwtService;

	public AuthController(JwtService jwtService)
	{
		_jwtService = jwtService;
	}
	[HttpPost("facebook")]
	public async Task<IActionResult> FacebookLogin([FromBody] FacebookLoginRequest request)
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

		var result = await _jwtService.AuthenticateWithFacebook(request.AccessToken);
		if (result == null)
		{
			return BadRequest("Facebook login thất bại");
		}

		// Lưu token vào cookie với tùy chọn HttpOnly
		var cookieOptions = new CookieOptions
		{
			HttpOnly = false, // Nếu muốn không cho phép JavaScript truy cập, hãy đặt thành true
			Secure = false,   // Nếu bạn đang dùng HTTPS, có thể đặt thành true
			Expires = DateTime.UtcNow.AddSeconds(result.ExpiresIn)
		};

		Response.Cookies.Append("accessToken", result.AccessToken, cookieOptions);

		return Ok(new { Message = "Facebook login successful", Token = result.AccessToken });
	}

}
