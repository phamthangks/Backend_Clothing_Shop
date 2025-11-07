using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Numerics;
using test1.Models;
using X.PagedList.Extensions;

namespace Test.Areas.Admin.Controllers
{

	[Area("admin")]
	[Route("api/product")]
	[ApiController]
	public class ProductsAPIController : ControllerBase
	{
		private readonly QlbanQuanAoContext _context;

		public ProductsAPIController(QlbanQuanAoContext context)
		{
			_context = context;
		}

		[HttpGet("all")]
		public IActionResult GetAllProducts()
		{
			var lstSanPham = _context.Products
				.Include(p => p.ProductImages)
				.Include(p => p.Category)
				.Include(p => p.Brand)
				.AsNoTracking()
				.ToList();
			return Ok(lstSanPham);
		}

		[HttpGet("paged")]
		public IActionResult GetProducts(int? page, int? pageSize)
		{
			int defaultPageSize = 6;
			int pageNumber = (page.HasValue && page.Value > 0) ? page.Value : 1;
			int effectivePageSize = (pageSize.HasValue && pageSize.Value > 0) ? pageSize.Value : defaultPageSize;

			var productsQuery = _context.Products
				.Include(p => p.ProductImages)
				.Include(p => p.Category)
				.Include(p => p.Brand)
				.AsNoTracking();
			var pagedProducts = productsQuery.ToPagedList(pageNumber, effectivePageSize);

			var response = new
			{
				items = pagedProducts.ToList(),
				total = pagedProducts.TotalItemCount
			};

			return Ok(response);
		}

		[HttpGet("{id}")]
		public IActionResult GetProductById(int id)
		{
			var product = _context.Products
				.Include(p => p.ProductImages)
				.Include(p => p.Category)
				.Include(p => p.Brand)
				.AsNoTracking()
				.FirstOrDefault(p => p.Id == id);

			if (product == null)
			{
				return NotFound(new { message = "Không tìm thấy sản phẩm" });
			}

			return Ok(product);
		}

		[HttpPost("Test")]
		public IActionResult Test([FromForm] IFormCollection form)
		{
			try
			{
				Console.WriteLine("=== Test FormData ===");
				foreach (var key in form.Keys)
				{
					Console.WriteLine($"{key}: {form[key]}");
				}
				return Ok(new { message = "FormData received successfully", keys = form.Keys.ToList() });
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Test error: {ex.Message}");
				return BadRequest(new { error = ex.Message });
			}
		}

		[HttpPost("Create")]
		public async Task<IActionResult> Create([FromForm] ProductCreateRequest request)
		{
			try
			{
				// Log để debug
				Console.WriteLine($"Received request: Name={request?.Name}, Price={request?.Price}");
				Console.WriteLine($"ThumbnailImage: {request?.ThumbnailImage?.FileName}");
				Console.WriteLine($"Images count: {request?.Images?.Count ?? 0}");

				if (!ModelState.IsValid)
				{
					Console.WriteLine($"ModelState errors: {string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))}");
					return BadRequest(ModelState);
				}

				// Tạo product mới
				var product = new Product
				{
					Name = request.Name,
					Price = request.Price,
					Description = request.Description,
					CategoryId = request.CategoryId,
					BrandId = request.BrandId,
					CreatedAt = DateTime.Now,
					UpdatedAt = DateTime.Now
				};

				// Upload ảnh đại diện nếu có
				if (request.ThumbnailImage != null)
				{
					var thumbnailUrl = await SaveImage(request.ThumbnailImage);
					product.Image = thumbnailUrl;
				}

				_context.Products.Add(product);
				await _context.SaveChangesAsync();

				// Upload và lưu các ảnh phụ nếu có
				if (request.Images != null && request.Images.Count > 0)
				{
					foreach (var imageFile in request.Images)
					{
						var imageUrl = await SaveImage(imageFile);
						var productImage = new ProductImage
						{
							ProductId = product.Id,
							ImageUrl = imageUrl
						};
						_context.ProductImages.Add(productImage);
					}
					await _context.SaveChangesAsync();
				}

				// Load lại product với includes
				var createdProduct = await _context.Products
					.Include(p => p.ProductImages)
					.Include(p => p.Category)
					.Include(p => p.Brand)
					.FirstOrDefaultAsync(p => p.Id == product.Id);

				return Ok(new
				{
					message = "Sản phẩm đã được tạo thành công",
					product = createdProduct
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi tạo sản phẩm",
					error = ex.Message
				});
			}
		}

		[HttpPut("Edit/{id}")]
		public async Task<IActionResult> Edit(int id, [FromForm] ProductUpdateRequest request)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				var existingProduct = await _context.Products
					.Include(p => p.ProductImages)
					.FirstOrDefaultAsync(p => p.Id == id);

				if (existingProduct == null)
				{
					return NotFound(new { message = $"Không tìm thấy sản phẩm với ID {id}" });
				}

				// Cập nhật thông tin cơ bản
				existingProduct.Name = request.Name;
				existingProduct.Price = request.Price;
				existingProduct.Description = request.Description;
				existingProduct.CategoryId = request.CategoryId;
				existingProduct.BrandId = request.BrandId;
				existingProduct.UpdatedAt = DateTime.Now;

				// Cập nhật ảnh đại diện nếu có ảnh mới
				if (request.ThumbnailImage != null)
				{
					var thumbnailUrl = await SaveImage(request.ThumbnailImage);
					existingProduct.Image = thumbnailUrl;
				}

				// Xử lý xóa ảnh phụ nếu có
				if (request.DeletedImageIds != null && request.DeletedImageIds.Count > 0)
				{
					var imagesToDelete = existingProduct.ProductImages
						.Where(pi => request.DeletedImageIds.Contains(pi.Id))
						.ToList();

					_context.ProductImages.RemoveRange(imagesToDelete);
				}

				// Thêm ảnh phụ mới nếu có
				if (request.Images != null && request.Images.Count > 0)
				{
					foreach (var imageFile in request.Images)
					{
						var imageUrl = await SaveImage(imageFile);
						var productImage = new ProductImage
						{
							ProductId = existingProduct.Id,
							ImageUrl = imageUrl
						};
						_context.ProductImages.Add(productImage);
					}
				}

				await _context.SaveChangesAsync();

				// Load lại product với includes
				var updatedProduct = await _context.Products
					.Include(p => p.ProductImages)
					.Include(p => p.Category)
					.Include(p => p.Brand)
					.AsNoTracking()
					.FirstOrDefaultAsync(p => p.Id == id);

				return Ok(new
				{
					success = true,
					message = "Sản phẩm đã được cập nhật thành công!",
					product = updatedProduct
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi cập nhật sản phẩm",
					error = ex.Message
				});
			}
		}

		[HttpDelete("Delete/{id}")]
		public async Task<IActionResult> Delete(int id)
		{
			try
			{
				var product = await _context.Products
					.Include(p => p.ProductImages)
					.FirstOrDefaultAsync(p => p.Id == id);

				if (product == null)
				{
					return NotFound(new { message = "Không tìm thấy sản phẩm" });
				}

				// Xóa các ảnh phụ
				_context.ProductImages.RemoveRange(product.ProductImages);

				// Xóa product
				_context.Products.Remove(product);
				await _context.SaveChangesAsync();

				return Ok(new { message = "Sản phẩm đã được xóa thành công" });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi xóa sản phẩm",
					error = ex.Message
				});
			}
		}

		#region Private Methods

	private async Task<string> SaveImage(IFormFile file)
	{
		var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");

		if (!Directory.Exists(uploadsFolder))
		{
			Directory.CreateDirectory(uploadsFolder);
		}

		var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
		var filePath = Path.Combine(uploadsFolder, uniqueFileName);

		using (var fileStream = new FileStream(filePath, FileMode.Create))
		{
			await file.CopyToAsync(fileStream);
		}

		return uniqueFileName;
	}

		#endregion
	}

	#region Request Models

	public class ProductCreateRequest
	{
		public string Name { get; set; } = null!;
		public double Price { get; set; }
		public string? Description { get; set; }
		public int? CategoryId { get; set; }
		public int? BrandId { get; set; }
		public IFormFile? ThumbnailImage { get; set; }
		public List<IFormFile>? Images { get; set; }
	}

	public class ProductUpdateRequest
	{
		public string Name { get; set; } = null!;
		public double Price { get; set; }
		public string? Description { get; set; }
		public int? CategoryId { get; set; }
		public int? BrandId { get; set; }
		public IFormFile? ThumbnailImage { get; set; }
		public List<IFormFile>? Images { get; set; }
		public List<int>? DeletedImageIds { get; set; }
	}

	#endregion
}
