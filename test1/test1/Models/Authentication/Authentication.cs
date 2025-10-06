using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using test1.Service;
using Azure;
using test1.Models.AccessModel;
using Microsoft.EntityFrameworkCore;

namespace test1.Models.Authentication
{
    public class Authentication : ActionFilterAttribute
    {
        private readonly JwtService _jwtService;
        QlbanQuanAoContext _context = new QlbanQuanAoContext();
        public override async void OnActionExecuting(ActionExecutingContext context)
        {
            // Lấy accessToken từ cookie
            var token = context.HttpContext.Request.Cookies["accessToken"];

            // Kiểm tra nếu không có token hoặc token bị null
            if (string.IsNullOrEmpty(token))
            {
                // Chuyển hướng đến trang đăng nhập nếu không có token
                context.Result = new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "Controller", "Account" },
                    { "Action", "Login" }
                });
                return;
            }

            // Giải mã JWT token để kiểm tra tính hợp lệ
            var handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwtToken;

            try
            {
                jwtToken = handler.ReadToken(token) as JwtSecurityToken;
            }
            catch
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "Controller", "Account" },
                    { "Action", "Login" }
                });
                return;
            }
            // Kiểm tra vai trò từ claims
            var roleClaim = _jwtService.GetUserDataFromToken(token);

            // Nếu là user, chuyển hướng đến trang Home và không cho phép vào Admin
            if (roleClaim.NameRole == "user")
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "Controller", "Home" },
                    { "Action", "Home" }
                });
                return;
            }

            // Nếu là admin, cho phép tiếp tục truy cập vào controller hiện tại
            if (roleClaim.NameRole == "admin")
            {
                context.Result = new RedirectToRouteResult(new RouteValueDictionary
                {
                    { "Area", "Admin" },
                    { "Controller", "HomeAdmin" },
                    { "Action", "Index" }
                });
                return;
            }
        }

    }
}
