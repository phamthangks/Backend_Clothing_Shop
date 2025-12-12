using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using test1.Models;
using test1.Service;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("QlbanQuanAoContext");
builder.Services.AddDbContext<QlbanQuanAoContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<PayPalService>();
builder.Services.AddSingleton<TwilioService>();
builder.Services.AddSingleton<ContractService>();
builder.Services.AddScoped<StatisticsPdfService>();
builder.Services.AddDistributedMemoryCache();

// ??ng k� session
builder.Services.AddSession();

//// Th�m Swagger cho vi?c ki?m th? API
builder.Services.AddSwaggerGen(options =>
{
	var jwtSecurityScheme = new OpenApiSecurityScheme
	{
		BearerFormat = "JWT",
		Name = "Authorization",
		In = ParameterLocation.Header,
		Type = SecuritySchemeType.Http,
		Scheme = JwtBearerDefaults.AuthenticationScheme,
		Description = "Nh?p token JWT c?a b?n",
		Reference = new OpenApiReference
		{
			Id = JwtBearerDefaults.AuthenticationScheme,
			Type = ReferenceType.SecurityScheme
		}
	};

	var cookieSecurityScheme = new OpenApiSecurityScheme
	{
		Name = "Cookie",
		Type = SecuritySchemeType.ApiKey,
		In = ParameterLocation.Cookie,
		Description = "X�c th?c Cookie"
	};

	// ??ng k� c? hai ph??ng th?c x�c th?c JWT v� Cookie trong Swagger
	options.AddSecurityDefinition("Bearer", jwtSecurityScheme);
	options.AddSecurityDefinition("Cookie", cookieSecurityScheme);

	options.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{ jwtSecurityScheme, Array.Empty<string>() },
		{ cookieSecurityScheme, Array.Empty<string>() }
	});
});

//// C?u h�nh CORS
//builder.Services.AddCors(options =>
//{
//	options.AddPolicy("AllowAngular",
//		policy => policy.AllowAnyOrigin()
//						.AllowAnyHeader()
//						.AllowAnyMethod());
//});
// Th�m d?ch v? CORS v� ??nh ngh?a policy
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowAngularClient", policy =>
	{
		policy.WithOrigins("https://localhost:4200") // Ch? cho ph�p Angular client tr�n ??a ch? n�y
			  .AllowAnyHeader()
			  .AllowAnyMethod()
			  .AllowCredentials(); // Cho ph�p g?i credentials (cookie, auth header,...)
	});
});
// C?u h�nh x�c th?c
builder.Services.AddAuthentication(options =>
{
	options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
	options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
	options.Cookie.Name = "accessToken";
	options.Cookie.HttpOnly = false;
	//options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
	//options.Cookie.SameSite = SameSiteMode.Strict;
	options.LoginPath = "/Account/Login";
})
.AddJwtBearer(options =>
{
	options.RequireHttpsMetadata = false;
	options.SaveToken = true;
	options.TokenValidationParameters = new TokenValidationParameters
	{
		ValidIssuer = builder.Configuration["JwtConfig:Issuer"],
		ValidAudience = builder.Configuration["JwtConfig:Audience"],
		IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtConfig:Key"])),
		ValidateIssuer = true,
		ValidateAudience = true,
		ValidateLifetime = false,
		ValidateIssuerSigningKey = true,
		RoleClaimType = ClaimTypes.Role
	};
});

// Th�m controller v� c?u h�nh JSON
//builder.Services.AddControllers().AddJsonOptions(options =>
//{
//	options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
//});
builder.Services.AddControllers().AddJsonOptions(options =>
{
	options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
	options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Cấu hình FormOptions để xử lý FormData
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
	options.ValueLengthLimit = int.MaxValue;
	options.MultipartBodyLengthLimit = int.MaxValue;
	options.MultipartHeadersLengthLimit = int.MaxValue;
	options.ValueCountLimit = int.MaxValue;
	options.KeyLengthLimit = int.MaxValue;
});

// Cấu hình Kestrel để xử lý file upload lớn
builder.WebHost.ConfigureKestrel(options =>
{
	options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

builder.WebHost.UseWebRoot("wwwroot");

// C?u h�nh Swagger v� Endpoints API Explorer
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
var app = builder.Build();

//app.UseCors("AllowAngular");
app.UseCors("AllowAngularClient");
// C?u h�nh pipeline HTTP request
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}
app.MapControllerRoute(
	name: "areas",
	pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
);
// S? d?ng session tr??c middleware x�c th?c
app.UseSession();
app.UseMiddleware<JwtRefreshTokenMiddleware>();
app.UseStaticFiles();
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
