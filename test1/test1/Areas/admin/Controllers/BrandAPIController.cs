using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test1.Models;

namespace test1.Areas.Admin.Controllers
{
    [Area("admin")]
    [Route("api/brand")]
    [ApiController]
    public class BrandAPIController : Controller
    {
        private readonly QlbanQuanAoContext _context;

        // Sử dụng Dependency Injection thay vì new instance
        public BrandAPIController(QlbanQuanAoContext context)
        {
            _context = context;
        }

		// GET: api/brand - Lấy danh sách có phân trang và tìm kiếm
		[HttpGet]
		public IActionResult GetAll(int page = 1, int size = 10, string? search = null)
		{
			try
			{
				if (page <= 0) page = 1;
				if (size <= 0) size = 10;

				var query = _context.Brands.AsQueryable();

				// Tìm kiếm theo tên (không phân biệt hoa thường)
				if (!string.IsNullOrWhiteSpace(search))
				{
					var keyword = search.Trim().ToLower();
					query = query.Where(b => b.Name != null && EF.Functions.Like(b.Name.ToLower(), $"%{keyword}%"));
				}

				var total = query.Count();
				var brands = query
					.OrderBy(b => b.Name)
					.Skip((page - 1) * size)
					.Take(size)
					.ToList();

				return Ok(new
				{
					data = brands,
					total = total,
					page = page,
					size = size
				});
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi tải danh sách brand", detail = ex.Message });
			}
		}

		// GET: api/brand/all - Lấy tất cả brands (không phân trang)
		[HttpGet("all")]
		public IActionResult GetAllBrands()
		{
			try
			{
				var brands = _context.Brands
					.OrderBy(b => b.Name)
					.ToList();
				return Ok(brands);
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi tải danh sách brand", detail = ex.Message });
			}
		}

		// GET: api/brand/{id} - Lấy brand theo ID
		[HttpGet("{id}")]
		public IActionResult GetById(int id)
		{
			try
			{
				if (id <= 0)
				{
					return BadRequest(new { message = "ID không hợp lệ" });
				}

				var brand = _context.Brands.Find(id);
				if (brand == null)
				{
					return NotFound(new { message = "Không tìm thấy brand" });
				}
				return Ok(brand);
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi tải brand", detail = ex.Message });
			}
		}

		// POST: api/brand/Create - Tạo brand mới
		[HttpPost("Create")]
        public IActionResult Create([FromBody] Brand brand)
        {
			// Validation 1: Kiểm tra tên có rỗng không
			if (string.IsNullOrWhiteSpace(brand.Name))
			{
				return BadRequest(new { message = "Tên brand là bắt buộc" });
			}

			// Validation 2: Kiểm tra độ dài tên
			if (brand.Name.Trim().Length > 255)
			{
				return BadRequest(new { message = "Tên brand không được vượt quá 255 ký tự" });
			}

			// Validation 3: Kiểm tra trùng tên (case-insensitive)
			bool nameExists = _context.Brands.Any(b =>
				b.Name != null && b.Name.ToLower() == brand.Name.Trim().ToLower());
			if (nameExists)
			{
				return Conflict(new { message = $"Brand '{brand.Name.Trim()}' đã tồn tại" });
			}

			try
			{
				// Trim tên để loại bỏ khoảng trắng thừa
				brand.Name = brand.Name.Trim();
				_context.Brands.Add(brand);
				_context.SaveChanges();
				
				return Ok(new
				{
					message = "Brand đã được tạo thành công",
					brandId = brand.Id,
					brand = brand
				});
			}
			catch (DbUpdateException ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi lưu dữ liệu", detail = ex.InnerException?.Message ?? ex.Message });
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi không xác định", detail = ex.Message });
			}
        }

		// PUT: api/brand/Edit/{id} - Cập nhật brand
		[HttpPut("Edit/{id}")]
		public IActionResult Edit(int id, [FromBody] Brand brand)
		{
			// Validation 1: Kiểm tra ID
			if (id <= 0)
			{
				return BadRequest(new { message = "ID không hợp lệ" });
			}

			var existingBrand = _context.Brands.Find(id);
			if (existingBrand == null)
			{
				return NotFound(new { message = "Brand không tồn tại" });
			}

			// Validation 2: Kiểm tra tên có rỗng không
			if (string.IsNullOrWhiteSpace(brand.Name))
			{
				return BadRequest(new { message = "Tên brand là bắt buộc" });
			}

			// Validation 3: Kiểm tra độ dài tên
			if (brand.Name.Trim().Length > 255)
			{
				return BadRequest(new { message = "Tên brand không được vượt quá 255 ký tự" });
			}

			// Validation 4: Kiểm tra trùng tên với brand khác (case-insensitive)
			bool nameExists = _context.Brands.Any(b =>
				b.Id != id && b.Name != null && b.Name.ToLower() == brand.Name.Trim().ToLower());
			if (nameExists)
			{
				return Conflict(new { message = $"Brand '{brand.Name.Trim()}' đã tồn tại" });
			}

			try
			{
				existingBrand.Name = brand.Name.Trim();
				_context.SaveChanges();
				
				return Ok(new
				{
					message = "Brand đã được cập nhật thành công!",
					brand = existingBrand
				});
			}
			catch (DbUpdateException ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi cập nhật dữ liệu", detail = ex.InnerException?.Message ?? ex.Message });
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi không xác định", detail = ex.Message });
			}
		}

		// DELETE: api/brand/Delete/{id} - Xóa brand
		[HttpDelete("Delete/{id}")]
        public IActionResult Delete(int id)
        {
			// Validation 1: Kiểm tra ID
			if (id <= 0)
			{
				return BadRequest(new { message = "ID không hợp lệ" });
			}

            var brand = _context.Brands.Find(id);
            if (brand == null)
            {
                return NotFound(new { message = "Không tìm thấy brand" });
            }

			// Validation 2: Kiểm tra xem có sản phẩm nào đang dùng brand này không
			bool hasProducts = _context.Products.Any(p => p.BrandId == id);
			if (hasProducts)
			{
				return BadRequest(new
				{
					message = "Không thể xóa brand này vì đang có sản phẩm sử dụng",
					detail = "Vui lòng xóa hoặc chuyển các sản phẩm sang brand khác trước khi xóa"
				});
			}

			try
			{
				_context.Brands.Remove(brand);
				_context.SaveChanges();
				return Ok(new { message = "Brand đã được xóa thành công" });
			}
			catch (DbUpdateException ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi xóa dữ liệu", detail = ex.InnerException?.Message ?? ex.Message });
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi không xác định", detail = ex.Message });
			}
        }
    }
}
