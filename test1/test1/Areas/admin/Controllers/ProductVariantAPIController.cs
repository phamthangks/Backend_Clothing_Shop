using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test1.Models;

namespace test1.Areas.Admin.Controllers
{
	[Area("admin")]
	[Route("api/productvariant")]
	[ApiController]
	public class ProductVariantAPIController : ControllerBase
	{
		private readonly QlbanQuanAoContext _context;

		public ProductVariantAPIController(QlbanQuanAoContext context)
		{
			_context = context;
		}

		// GET: api/productvariant/all
		[HttpGet("all")]
		public async Task<IActionResult> GetAllVariants()
		{
			try
			{
				var variants = await _context.ProductVariants
					.Include(v => v.Product)
						.ThenInclude(p => p.Category)
					.Include(v => v.Product)
						.ThenInclude(p => p.Brand)
					.AsNoTracking()
					.ToListAsync();

				return Ok(variants);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi lấy danh sách biến thể",
					error = ex.Message
				});
			}
		}

		// GET: api/productvariant/{id}
		[HttpGet("{id}")]
		public async Task<IActionResult> GetVariantById(int id)
		{
			try
			{
				var variant = await _context.ProductVariants
					.Include(v => v.Product)
						.ThenInclude(p => p.Category)
					.Include(v => v.Product)
						.ThenInclude(p => p.Brand)
					.AsNoTracking()
					.FirstOrDefaultAsync(v => v.Id == id);

				if (variant == null)
				{
					return NotFound(new { message = "Không tìm thấy biến thể sản phẩm" });
				}

				return Ok(variant);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi lấy thông tin biến thể",
					error = ex.Message
				});
			}
		}

		// GET: api/productvariant/product/{productId}
		[HttpGet("product/{productId}")]
		public async Task<IActionResult> GetVariantsByProductId(int productId)
		{
			try
			{
				var variants = await _context.ProductVariants
					.Include(v => v.Product)
					.Where(v => v.ProductId == productId)
					.AsNoTracking()
					.ToListAsync();

				return Ok(variants);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi lấy danh sách biến thể của sản phẩm",
					error = ex.Message
				});
			}
		}

		// POST: api/productvariant/create
		[HttpPost("create")]
		public async Task<IActionResult> CreateVariant([FromBody] ProductVariantCreateRequest request)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				// Kiểm tra sản phẩm có tồn tại không
				var product = await _context.Products.FindAsync(request.ProductId);
				if (product == null)
				{
					return NotFound(new { message = "Không tìm thấy sản phẩm" });
				}

				// Kiểm tra xem biến thể đã tồn tại chưa (cùng màu và size cho cùng 1 sản phẩm)
				var existingVariant = await _context.ProductVariants
					.FirstOrDefaultAsync(v => 
						v.ProductId == request.ProductId && 
						v.Color.ToLower() == request.Color.ToLower() && 
						v.Size.ToLower() == request.Size.ToLower());

				if (existingVariant != null)
				{
					return BadRequest(new { message = "Biến thể với màu và kích thước này đã tồn tại" });
				}

				var variant = new ProductVariant
				{
					ProductId = request.ProductId,
					Color = request.Color,
					Size = request.Size,
					Price = request.Price,
					StockQuantity = request.StockQuantity
				};

				_context.ProductVariants.Add(variant);
				await _context.SaveChangesAsync();

				// Load lại với includes
				var createdVariant = await _context.ProductVariants
					.Include(v => v.Product)
						.ThenInclude(p => p.Category)
					.Include(v => v.Product)
						.ThenInclude(p => p.Brand)
					.FirstOrDefaultAsync(v => v.Id == variant.Id);

				return Ok(new
				{
					message = "Tạo biến thể sản phẩm thành công",
					variant = createdVariant
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi tạo biến thể sản phẩm",
					error = ex.Message
				});
			}
		}

		// PUT: api/productvariant/edit/{id}
		[HttpPut("edit/{id}")]
		public async Task<IActionResult> UpdateVariant(int id, [FromBody] ProductVariantUpdateRequest request)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				var variant = await _context.ProductVariants.FindAsync(id);
				if (variant == null)
				{
					return NotFound(new { message = "Không tìm thấy biến thể sản phẩm" });
				}

				// Kiểm tra xem biến thể mới có bị trùng không (trừ chính nó)
				var duplicateVariant = await _context.ProductVariants
					.FirstOrDefaultAsync(v => 
						v.Id != id &&
						v.ProductId == variant.ProductId && 
						v.Color.ToLower() == request.Color.ToLower() && 
						v.Size.ToLower() == request.Size.ToLower());

				if (duplicateVariant != null)
				{
					return BadRequest(new { message = "Biến thể với màu và kích thước này đã tồn tại" });
				}

				// Cập nhật thông tin
				variant.Color = request.Color;
				variant.Size = request.Size;
				variant.Price = request.Price;
				variant.StockQuantity = request.StockQuantity;

				await _context.SaveChangesAsync();

				// Load lại với includes
				var updatedVariant = await _context.ProductVariants
					.Include(v => v.Product)
						.ThenInclude(p => p.Category)
					.Include(v => v.Product)
						.ThenInclude(p => p.Brand)
					.AsNoTracking()
					.FirstOrDefaultAsync(v => v.Id == id);

				return Ok(new
				{
					message = "Cập nhật biến thể sản phẩm thành công",
					variant = updatedVariant
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi cập nhật biến thể sản phẩm",
					error = ex.Message
				});
			}
		}

		// DELETE: api/productvariant/delete/{id}
		[HttpDelete("delete/{id}")]
		public async Task<IActionResult> DeleteVariant(int id)
		{
			try
			{
				var variant = await _context.ProductVariants.FindAsync(id);
				if (variant == null)
				{
					return NotFound(new { message = "Không tìm thấy biến thể sản phẩm" });
				}

				// Xóa biến thể (lưu ý: trong tương lai nên thêm ProductVariantId vào OrderDetail)
				_context.ProductVariants.Remove(variant);
				await _context.SaveChangesAsync();

				return Ok(new { message = "Xóa biến thể sản phẩm thành công" });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi xóa biến thể sản phẩm",
					error = ex.Message
				});
			}
		}

		// POST: api/productvariant/create-batch
		[HttpPost("create-batch")]
		public async Task<IActionResult> CreateBatchVariants([FromBody] ProductVariantBatchCreateRequest request)
		{
			try
			{
				if (!ModelState.IsValid)
				{
					return BadRequest(ModelState);
				}

				// Kiểm tra sản phẩm có tồn tại không
				var product = await _context.Products.FindAsync(request.ProductId);
				if (product == null)
				{
					return NotFound(new { message = "Không tìm thấy sản phẩm" });
				}

				var createdVariants = new List<ProductVariant>();
				var errors = new List<string>();

				foreach (var variantData in request.Variants)
				{
					// Kiểm tra xem biến thể đã tồn tại chưa
					var existingVariant = await _context.ProductVariants
						.FirstOrDefaultAsync(v => 
							v.ProductId == request.ProductId && 
							v.Color.ToLower() == variantData.Color.ToLower() && 
							v.Size.ToLower() == variantData.Size.ToLower());

					if (existingVariant != null)
					{
						errors.Add($"Biến thể {variantData.Color} - {variantData.Size} đã tồn tại");
						continue;
					}

					var variant = new ProductVariant
					{
						ProductId = request.ProductId,
						Color = variantData.Color,
						Size = variantData.Size,
						Price = variantData.Price,
						StockQuantity = variantData.StockQuantity
					};

					_context.ProductVariants.Add(variant);
					createdVariants.Add(variant);
				}

				if (createdVariants.Count > 0)
				{
					await _context.SaveChangesAsync();
				}

				return Ok(new
				{
					message = $"Đã tạo {createdVariants.Count} biến thể thành công",
					createdCount = createdVariants.Count,
					totalRequested = request.Variants.Count,
					errors = errors,
					variants = createdVariants
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new
				{
					message = "Lỗi khi tạo hàng loạt biến thể",
					error = ex.Message
				});
			}
		}
	}

	#region Request Models

	public class ProductVariantCreateRequest
	{
		public int ProductId { get; set; }
		public string Color { get; set; } = null!;
		public string Size { get; set; } = null!;
		public double Price { get; set; }
		public int StockQuantity { get; set; }
	}

	public class ProductVariantUpdateRequest
	{
		public string Color { get; set; } = null!;
		public string Size { get; set; } = null!;
		public double Price { get; set; }
		public int StockQuantity { get; set; }
	}

	public class ProductVariantBatchCreateRequest
	{
		public int ProductId { get; set; }
		public List<VariantData> Variants { get; set; } = new List<VariantData>();
	}

	public class VariantData
	{
		public string Color { get; set; } = null!;
		public string Size { get; set; } = null!;
		public double Price { get; set; }
		public int StockQuantity { get; set; }
	}

	#endregion
}

