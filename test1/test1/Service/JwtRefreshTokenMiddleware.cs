using Microsoft.AspNetCore.Http;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using test1.Service;
using test1.Models.AccessModel;
using Microsoft.EntityFrameworkCore;
using test1.Models;

public class JwtRefreshTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public JwtRefreshTokenMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Lấy accessToken từ cookie
        var token = context.Request.Cookies["accessToken"];
        if (string.IsNullOrEmpty(token))
        {
            await _next(context);
            return;
        }

        var handler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwtToken;

        try
        {
            jwtToken = handler.ReadToken(token) as JwtSecurityToken;
        }
        catch
        {
            context.Response.Redirect("/Account/Login");
            return;
        }

        // Nếu token đã hết hạn
        if (jwtToken != null && jwtToken.ValidTo < DateTime.UtcNow.AddMinutes(1))
        {
            // Tạo một scope mới để sử dụng các dịch vụ scoped
            using var scope = _serviceScopeFactory.CreateScope();
            var jwtService = scope.ServiceProvider.GetRequiredService<JwtService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<QlbanQuanAoContext>();

            // Lấy thông tin người dùng từ token
            var userData = jwtService.GetUserDataFromToken(token);
            if (userData == null)
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == userData.PhoneNumber);
            if (user == null)
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            var refreshTokenRecord = await dbContext.Tokens
                .Where(o => o.UserId == user.Id && !o.Revoked && !o.Expired)
                .Select(o => o.Token1)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(refreshTokenRecord))
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            // Làm mới token
            var newToken = await jwtService.RefreshToken(refreshTokenRecord);
            if (newToken != null)
            {
                context.Response.Cookies.Append("accessToken", newToken.AccessToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddSeconds(newToken.ExpiresIn)
                });
            }
            else
            {
                context.Response.Redirect("/Account/Login");
                return;
            }
        }

        await _next(context);
    }
}
