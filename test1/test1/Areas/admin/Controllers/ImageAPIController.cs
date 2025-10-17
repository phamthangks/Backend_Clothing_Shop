using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using test1.Models;

namespace Test.Areas.Admin.Controllers
{
	[Area("admin")]
	[Route("api/image")]
	[ApiController]
	public class ImageAPIController : ControllerBase
	{
		private readonly QlbanQuanAoContext _context;
		private readonly IWebHostEnvironment _env;
		private readonly string _uploadPath;
		private readonly long _maxFileSize = 5 * 1024 * 1024; // 5MB
		private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

		public ImageAPIController(QlbanQuanAoContext context, IWebHostEnvironment env)
		{
			_context = context;
			_env = env;
			_uploadPath = Path.Combine(_env.WebRootPath, "uploads");

			// Tạo thư mục uploads nếu chưa tồn tại
			if (!Directory.Exists(_uploadPath))
			{
				Directory.CreateDirectory(_uploadPath);
			}
		}

		/// <summary>
		/// Upload một ảnh đơn (ảnh đại diện)
		/// </summary>
		[HttpPost("upload-single")]
		public async Task<IActionResult> UploadSingleImage(IFormFile file)
		{
			try
			{
				var validationResult = ValidateImage(file);
				if (!validationResult.IsValid)
				{
					return BadRequest(new { message = validationResult.ErrorMessage });
				}

				// Tính hash để check trùng
				var fileHash = await CalculateFileHash(file);
				var existingImage = CheckDuplicateImage(fileHash);

				if (existingImage != null)
				{
					return Ok(new
					{
						message = "Ảnh đã tồn tại trong hệ thống",
						imageUrl = existingImage,
						isDuplicate = true
					});
				}

				// Lưu file
				var fileName = await SaveFile(file, fileHash);
				var imageUrl = $"/uploads/{fileName}";

				return Ok(new
				{
					message = "Upload ảnh thành công",
					imageUrl = imageUrl,
					isDuplicate = false
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Lỗi khi upload ảnh", error = ex.Message });
			}
		}

		/// <summary>
		/// Upload nhiều ảnh
		/// </summary>
		[HttpPost("upload-multiple")]
		public async Task<IActionResult> UploadMultipleImages([FromForm] List<IFormFile> files)
		{
			try
			{
				if (files == null || files.Count == 0)
				{
					return BadRequest(new { message = "Không có file nào được chọn" });
				}

				var results = new List<object>();

				foreach (var file in files)
				{
					var validationResult = ValidateImage(file);
					if (!validationResult.IsValid)
					{
						results.Add(new
						{
							fileName = file.FileName,
							success = false,
							message = validationResult.ErrorMessage
						});
						continue;
					}

					// Tính hash để check trùng
					var fileHash = await CalculateFileHash(file);
					var existingImage = CheckDuplicateImage(fileHash);

					if (existingImage != null)
					{
						results.Add(new
						{
							fileName = file.FileName,
							success = true,
							imageUrl = existingImage,
							isDuplicate = true,
							message = "Ảnh đã tồn tại"
						});
						continue;
					}

					// Lưu file
					var fileName = await SaveFile(file, fileHash);
					var imageUrl = $"/uploads/{fileName}";

					results.Add(new
					{
						fileName = file.FileName,
						success = true,
						imageUrl = imageUrl,
						isDuplicate = false,
						message = "Upload thành công"
					});
				}

				return Ok(new
				{
					message = "Hoàn tất upload",
					results = results
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Lỗi khi upload ảnh", error = ex.Message });
			}
		}

		/// <summary>
		/// Xóa ảnh
		/// </summary>
		[HttpDelete("delete")]
		public IActionResult DeleteImage([FromQuery] string imageUrl)
		{
			try
			{
				if (string.IsNullOrEmpty(imageUrl))
				{
					return BadRequest(new { message = "Đường dẫn ảnh không hợp lệ" });
				}

				// Lấy tên file từ URL
				var fileName = Path.GetFileName(imageUrl);
				var filePath = Path.Combine(_uploadPath, fileName);

				if (!System.IO.File.Exists(filePath))
				{
					return NotFound(new { message = "Không tìm thấy file" });
				}

				// Check xem ảnh có đang được sử dụng không
				var isUsedInProducts = _context.Products.Any(p => p.Image == imageUrl);
				var isUsedInProductImages = _context.ProductImages.Any(pi => pi.ImageUrl == imageUrl);

				if (isUsedInProducts || isUsedInProductImages)
				{
					return BadRequest(new { message = "Ảnh đang được sử dụng, không thể xóa" });
				}

				// Xóa file
				System.IO.File.Delete(filePath);

				return Ok(new { message = "Xóa ảnh thành công" });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Lỗi khi xóa ảnh", error = ex.Message });
			}
		}

		/// <summary>
		/// Lấy danh sách tất cả ảnh đã upload
		/// </summary>
		[HttpGet("list")]
		public IActionResult GetAllImages()
		{
			try
			{
				var files = Directory.GetFiles(_uploadPath)
					.Select(f => new
					{
						fileName = Path.GetFileName(f),
						imageUrl = $"/uploads/{Path.GetFileName(f)}",
						size = new FileInfo(f).Length,
						createdDate = System.IO.File.GetCreationTime(f)
					})
					.OrderByDescending(f => f.createdDate)
					.ToList();

				return Ok(files);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Lỗi khi lấy danh sách ảnh", error = ex.Message });
			}
		}

		#region Private Methods

		/// <summary>
		/// Validate ảnh (kích thước, định dạng)
		/// </summary>
		private (bool IsValid, string ErrorMessage) ValidateImage(IFormFile file)
		{
			if (file == null || file.Length == 0)
			{
				return (false, "File không hợp lệ");
			}

			// Check kích thước
			if (file.Length > _maxFileSize)
			{
				return (false, $"Kích thước file vượt quá {_maxFileSize / 1024 / 1024}MB");
			}

			// Check định dạng
			var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
			if (!_allowedExtensions.Contains(extension))
			{
				return (false, $"Chỉ chấp nhận các định dạng: {string.Join(", ", _allowedExtensions)}");
			}

			// Check content type
			if (!file.ContentType.StartsWith("image/"))
			{
				return (false, "File không phải là ảnh");
			}

			return (true, string.Empty);
		}

		/// <summary>
		/// Tính hash của file để check trùng
		/// </summary>
		private async Task<string> CalculateFileHash(IFormFile file)
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = file.OpenReadStream())
				{
					var hash = await md5.ComputeHashAsync(stream);
					return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				}
			}
		}

		/// <summary>
		/// Check ảnh trùng dựa trên hash
		/// </summary>
		private string? CheckDuplicateImage(string fileHash)
		{
			// Check trong thư mục uploads xem có file nào có hash này chưa
			var files = Directory.GetFiles(_uploadPath);
			foreach (var filePath in files)
			{
				var fileName = Path.GetFileName(filePath);
				// Hash được lưu trong tên file (format: hash_originalname.ext)
				if (fileName.StartsWith(fileHash))
				{
					return $"/uploads/{fileName}";
				}
			}
			return null;
		}

		/// <summary>
		/// Lưu file với tên duy nhất
		/// </summary>
		private async Task<string> SaveFile(IFormFile file, string fileHash)
		{
			var extension = Path.GetExtension(file.FileName);
			var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
			
			// Format: hash_timestamp_originalname.ext
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var fileName = $"{fileHash}_{timestamp}_{originalFileName}{extension}";
			
			var filePath = Path.Combine(_uploadPath, fileName);

			using (var stream = new FileStream(filePath, FileMode.Create))
			{
				await file.CopyToAsync(stream);
			}

			return fileName;
		}

		#endregion
	}
}

