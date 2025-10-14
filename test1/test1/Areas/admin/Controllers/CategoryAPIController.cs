using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test1.Models;

namespace test1.Areas.Admin.Controllers
{
	[Area("admin")]
	[Route("api/category")]
	[ApiController]
	public class CategoryAPIController : Controller
	{
		private readonly QlbanQuanAoContext _context;

		// Sử dụng Dependency Injection thay vì new instance
		public CategoryAPIController(QlbanQuanAoContext context)
		{
			_context = context;
		}

		// GET: api/category - Lấy danh sách có phân trang và tìm kiếm
		[HttpGet]
		public IActionResult GetAll(int page = 1, int size = 10, string? search = null)
		{
			try
			{
				if (page <= 0) page = 1;
				if (size <= 0) size = 10;

				var query = _context.Categories.AsQueryable();

				// Tìm kiếm theo tên (không phân biệt hoa thường)
				if (!string.IsNullOrWhiteSpace(search))
				{
					var keyword = search.Trim().ToLower();
					query = query.Where(c => c.Name != null && EF.Functions.Like(c.Name.ToLower(), $"%{keyword}%"));
				}

				var total = query.Count();
				var categories = query
					.OrderBy(c => c.Name)
					.Skip((page - 1) * size)
					.Take(size)
					.ToList();

				return Ok(new
				{
					data = categories,
					total = total,
					page = page,
					size = size
				});
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi tải danh sách category", detail = ex.Message });
			}
		}

		// GET: api/category/all - Lấy tất cả categories (không phân trang)
		[HttpGet("all")]
		public IActionResult GetAllCategories()
		{
			try
			{
				var categories = _context.Categories
					.OrderBy(c => c.Name)
					.ToList();
				return Ok(categories);
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi tải danh sách category", detail = ex.Message });
			}
		}

		// GET: api/category/{id} - Lấy category theo ID
		[HttpGet("{id}")]
		public IActionResult GetById(int id)
		{
			try
			{
				if (id <= 0)
				{
					return BadRequest(new { message = "ID không hợp lệ" });
				}

				var category = _context.Categories.Find(id);
				if (category == null)
				{
					return NotFound(new { message = "Không tìm thấy category" });
				}
				return Ok(category);
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError,
					new { message = "Lỗi khi tải category", detail = ex.Message });
			}
		}

		// POST: api/category/Create - Tạo category mới
		[HttpPost("Create")]
		public IActionResult Create([FromBody] Category category)
		{
			// Validation 1: Kiểm tra tên có rỗng không
			if (string.IsNullOrWhiteSpace(category.Name))
			{
				return BadRequest(new { message = "Tên category là bắt buộc" });
			}

			// Validation 2: Kiểm tra độ dài tên
			if (category.Name.Trim().Length > 255)
			{
				return BadRequest(new { message = "Tên category không được vượt quá 255 ký tự" });
			}

			// Validation 3: Kiểm tra trùng tên (case-insensitive)
			bool nameExists = _context.Categories.Any(c =>
				c.Name != null && c.Name.ToLower() == category.Name.Trim().ToLower());
			if (nameExists)
			{
				return Conflict(new { message = $"Category '{category.Name.Trim()}' đã tồn tại" });
			}

			try
			{
				// Trim tên để loại bỏ khoảng trắng thừa
				category.Name = category.Name.Trim();
				_context.Categories.Add(category);
				_context.SaveChanges();
				
				return Ok(new
				{
					message = "Category đã được tạo thành công",
					categoryId = category.Id,
					category = category
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

		// PUT: api/category/Edit/{id} - Cập nhật category
		[HttpPut("Edit/{id}")]
		public IActionResult Edit(int id, [FromBody] Category category)
		{
			// Validation 1: Kiểm tra ID
			if (id <= 0)
			{
				return BadRequest(new { message = "ID không hợp lệ" });
			}

			var existingCategory = _context.Categories.Find(id);
			if (existingCategory == null)
			{
				return NotFound(new { message = "Category không tồn tại" });
			}

			// Validation 2: Kiểm tra tên có rỗng không
			if (string.IsNullOrWhiteSpace(category.Name))
			{
				return BadRequest(new { message = "Tên category là bắt buộc" });
			}

			// Validation 3: Kiểm tra độ dài tên
			if (category.Name.Trim().Length > 255)
			{
				return BadRequest(new { message = "Tên category không được vượt quá 255 ký tự" });
			}

			// Validation 4: Kiểm tra trùng tên với category khác (case-insensitive)
			bool nameExists = _context.Categories.Any(c =>
				c.Id != id && c.Name != null && c.Name.ToLower() == category.Name.Trim().ToLower());
			if (nameExists)
			{
				return Conflict(new { message = $"Category '{category.Name.Trim()}' đã tồn tại" });
			}

			try
			{
				existingCategory.Name = category.Name.Trim();
				_context.SaveChanges();
				
				return Ok(new
				{
					message = "Category đã được cập nhật thành công!",
					category = existingCategory
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

		// DELETE: api/category/Delete/{id} - Xóa category
		[HttpDelete("Delete/{id}")]
		public IActionResult Delete(int id)
		{
			// Validation 1: Kiểm tra ID
			if (id <= 0)
			{
				return BadRequest(new { message = "ID không hợp lệ" });
			}

			var category = _context.Categories.Find(id);
			if (category == null)
			{
				return NotFound(new { message = "Không tìm thấy category" });
			}

			// Validation 2: Kiểm tra xem có sản phẩm nào đang dùng category này không
			bool hasProducts = _context.Products.Any(p => p.CategoryId == id);
			if (hasProducts)
			{
				return BadRequest(new
				{
					message = "Không thể xóa category này vì đang có sản phẩm sử dụng",
					detail = "Vui lòng xóa hoặc chuyển các sản phẩm sang category khác trước khi xóa"
				});
			}

			try
			{
				_context.Categories.Remove(category);
				_context.SaveChanges();
				return Ok(new { message = "Category đã được xóa thành công" });
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
