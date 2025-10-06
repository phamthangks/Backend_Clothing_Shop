using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using test1.Handlers;
using test1.Models;
using test1.Models.AccessModel;
using test1.Models.Authentication;

namespace test1.Service
{
    public class JwtService
    {
        private readonly QlbanQuanAoContext _context;
        private readonly IConfiguration _configuration;

        public JwtService(QlbanQuanAoContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        public async Task<LoginResponseModel?> Authenticate(LoginRequestModel request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
                return null;

            var userAccount = await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber);

            if (userAccount is null || !PasswordHashHandler.VerifyPassword(request.Password, userAccount.Password))
                return null;

            var accessToken = GenerateAccessToken(userAccount);
            var refreshToken = await GenerateRefreshToken(userAccount);

            return new LoginResponseModel
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token1,
                PhoneNumber = request.PhoneNumber,
                ExpiresIn = (int)DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtConfig:TokenValidityMins"))
                    .Subtract(DateTime.UtcNow).TotalSeconds
            };
        }

        private string GenerateAccessToken(User user)
        {
            var issuer = _configuration["JwtConfig:Issuer"];
            var audience = _configuration["JwtConfig:Audience"];
            var key = _configuration["JwtConfig:Key"];
            var tokenValidityMins = _configuration.GetValue<int>("JwtConfig:TokenValidityMins");

            var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Name, user.PhoneNumber),
			new Claim(ClaimTypes.Role, user.Role?.Name ?? "user")

		};

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(tokenValidityMins),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha512Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(securityToken);
        }

        private async Task<Token> GenerateRefreshToken(User user)
        {
            var refreshTokenValidityDays = _configuration.GetValue<int>("JwtConfig:RefreshTokenValidityDays");
            var refreshToken = new Token
            {
                TokenType = "RefreshToken",
                Token1 = Guid.NewGuid().ToString(),
                UserId = user.Id,
                ExpirationDate = DateTime.UtcNow.AddDays(refreshTokenValidityDays),
                Revoked = false,
                Expired = false
            };

            _context.Tokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        public async Task<LoginResponseModel?> RefreshToken(string token)
        {
            var existingToken = await _context.Tokens
                .FirstOrDefaultAsync(t => t.Token1 == token && !t.Revoked && !t.Expired);

            if (existingToken == null || existingToken.ExpirationDate <= DateTime.UtcNow)
                return null;

            var user = await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == existingToken.UserId);

            if (user == null)
                return null;

            // Tạo mới Access Token và Refresh Token
            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = await GenerateRefreshToken(user);

            // Đánh dấu token cũ là đã hết hạn
            existingToken.Expired = true;
            existingToken.Revoked = true;
            await _context.SaveChangesAsync();

            return new LoginResponseModel
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token1,
                PhoneNumber = user.PhoneNumber,
                ExpiresIn = (int)DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtConfig:TokenValidityMins"))
                    .Subtract(DateTime.UtcNow).TotalSeconds
            };
        }
        public async Task RevokeAllTokens(string phoneNumber)
        {
            // Tìm tất cả các Refresh Token của người dùng dựa trên số điện thoại
            var user = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            if (user == null) return;

            var tokens = await _context.Tokens
                .Where(t => t.UserId == user.Id && !t.Revoked && !t.Expired)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.Revoked = true;
                token.Expired = true;
            }

            await _context.SaveChangesAsync();
        }

        public UserId GetUserDataFromToken(string token)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken == null)
            {
                return null;
            }

            // Lấy thông tin từ claims trong token
            var phoneNumber = jwtToken?.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value;
            var role = jwtToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            return new UserId
            {
                PhoneNumber = phoneNumber,
                NameRole = role
            };
        }
        public async Task<LoginResponseModel?> Register(RegisterRequestModel request)
        {
            // Tạo người dùng mới
            var user = new User
            {
                Fullname = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Password= PasswordHashHandler.HashPassword(request.Password),
                CreatedAt=DateTime.Now,
                RoleId=2
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
			// Đăng nhập tự động sau khi đăng ký thành công và trả về token
			return new LoginResponseModel
			{
				PhoneNumber = user.PhoneNumber,
				AccessToken = null,   // Không có token vì không đăng nhập tự động
				RefreshToken = null,
				ExpiresIn = 0         // Thời gian hết hạn token là 0 vì không có token
			};
		}
		//public async Task<LoginResponseModel?> Register(RegisterRequestModel request)
		//{
		//	// Tạo người dùng mới
		//	var user = new User
		//	{
		//		Fullname = request.FullName,
		//		PhoneNumber = request.PhoneNumber,
		//		Password = PasswordHashHandler.HashPassword(request.Password),
		//		CreatedAt = DateTime.Now,
		//		RoleId = 2
		//	};
		//	_context.Users.Add(user);
		//	await _context.SaveChangesAsync();
		//	// Đăng nhập tự động sau khi đăng ký thành công và trả về token
		//	return await Authenticate(new LoginRequestModel
		//	{
		//		PhoneNumber = user.PhoneNumber,
		//		Password = request.Password
		//	});
		//}
		public async Task<LoginResponseModel?> AuthenticateWithFacebook(string fbAccessToken)
        {
            // Gọi Facebook Graph API để xác thực access token và lấy thông tin user
            using var httpClient = new HttpClient();
            var fbUserInfoUrl = $"https://graph.facebook.com/me?fields=id,name,email&access_token={fbAccessToken}";
            var fbResponse = await httpClient.GetAsync(fbUserInfoUrl);
            if (!fbResponse.IsSuccessStatusCode)
            {
                // Nếu token không hợp lệ hoặc xảy ra lỗi, trả về null hoặc xử lý lỗi theo yêu cầu
                return null;
            }

            var fbResponseContent = await fbResponse.Content.ReadAsStringAsync();
            dynamic fbUser = JsonConvert.DeserializeObject(fbResponseContent);

            // Lấy thông tin từ Facebook
            string email = fbUser.email;
            string name = fbUser.name;
            string facebookId = fbUser.id;

            // Tìm kiếm người dùng trong CSDL dựa trên email
            var userAccount = await _context.Users.Include(u => u.Role)
                .FirstOrDefaultAsync(x => x.Email == email);

            // Nếu chưa có user, đăng ký tự động
            if (userAccount is null)
            {
                userAccount = new User
                {
                    Fullname = name,
                    Email = email,
					PhoneNumber = "",
					Password = "", // Không cần password cho đăng nhập Facebook
                    CreatedAt = DateTime.UtcNow,
					FacebookAccountId=1,
					RoleId = 2 // Giả sử role mặc định là 2
                };
                _context.Users.Add(userAccount);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Nếu đã tồn tại, cập nhật lại thông tin người dùng nếu cần (ví dụ: cập nhật tên)
                if (userAccount.Fullname != name)
                {
                    userAccount.Fullname = name;
                    _context.Users.Update(userAccount);
                    await _context.SaveChangesAsync();
                }
            }

            // Xử lý bảng social_accounts theo cấu trúc: id, provider, provider_id, email, name, user_id
            var socialAccount = await _context.SocialAccounts
                .FirstOrDefaultAsync(sa => sa.UserId == userAccount.Id && sa.Provider == "facebook");

            if (socialAccount != null)
            {
                // Nếu đã tồn tại, cập nhật thông tin từ Facebook
                socialAccount.ProviderId = facebookId;
                socialAccount.Email = email;
                socialAccount.Name = name;
                _context.SocialAccounts.Update(socialAccount);
            }
            else
            {
                // Nếu chưa tồn tại, tạo mới bản ghi social account
                socialAccount = new SocialAccount
                {
                    Provider = "facebook",
                    ProviderId = facebookId,
                    Email = email,
                    Name = name,
                    UserId = userAccount.Id
                };
                _context.SocialAccounts.Add(socialAccount);
            }
            await _context.SaveChangesAsync();

            // Sinh JWT sử dụng các phương thức GenerateAccessToken và GenerateRefreshToken hiện có
            var accessToken = GenerateAccessToken(userAccount);
            var refreshToken = await GenerateRefreshToken(userAccount);

            return new LoginResponseModel
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token1,
                PhoneNumber = userAccount.PhoneNumber, // Có thể dùng email nếu cần
                ExpiresIn = (int)DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtConfig:TokenValidityMins"))
                                .Subtract(DateTime.UtcNow).TotalSeconds
            };
        }

    }
}
