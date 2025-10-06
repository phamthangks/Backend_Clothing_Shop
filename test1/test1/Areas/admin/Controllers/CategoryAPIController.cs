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
		private readonly QlbanQuanAoContext _context = new QlbanQuanAoContext();

		[HttpGet]
		public IActionResult GetAll(int page = 1, int size = 10)
		{
			var categories = _context.Categories
				.Skip((page - 1) * size)
				.Take(size)
				.ToList();

			var total = _context.Categories.Count();

			return Ok(new
			{
				data = categories,
				total = total,
				page = page,
				size = size
			});
		}


		[HttpGet("paged")]

		[HttpPost("Create")]
		public IActionResult Create([FromBody] Category category)
		{
			if (ModelState.IsValid)
			{
				_context.Categories.Add(category);
				_context.SaveChanges();
				return Ok(new { message = "category đã được tạo thành công", categoryId = category.Id });
			}
			return BadRequest(ModelState);
		}
		[HttpPut("Edit/{id}")]
		public IActionResult Edit(int id, [FromBody] Category category)
		{
			var existingcategory = _context.Categories.Find(id);
			if (existingcategory == null)
			{
				return NotFound(new { message = "category không tồn tại" });
			}

			existingcategory.Name = category.Name;
			// Các thuộc tính khác nếu cần cập nhật

			_context.SaveChanges();
			return Ok(new { message = "category đã được cập nhật thành công!" });
		}


		[HttpDelete("Delete/{id}")]
		public IActionResult Delete(int id)
		{
			var category = _context.Categories.Find(id);
			if (category == null)
			{
				return NotFound(new { message = "Không tìm thấy " });
			}

			_context.Categories.Remove(category);
			_context.SaveChanges();

			return Ok(new { message = "category đã được xóa thành công" });
		}
	}
}
