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
        private readonly QlbanQuanAoContext _context = new QlbanQuanAoContext();

		[HttpGet]
		public IActionResult GetAll(int page = 1, int size = 10)
		{
			var brands = _context.Brands
				.Skip((page - 1) * size)
				.Take(size)
				.ToList();

			var total = _context.Brands.Count();

			return Ok(new
			{
				data = brands,
				total = total,
				page = page,
				size = size
			});
		}

	
		[HttpGet("paged")]

		[HttpPost("Create")]
        public IActionResult Create([FromBody] Brand brand)
        {
            if (ModelState.IsValid)
            {
                _context.Brands.Add(brand);
                _context.SaveChanges();
                return Ok(new { message = "Brand đã được tạo thành công", brandId = brand.Id });
            }
            return BadRequest(ModelState);
        }
		[HttpPut("Edit/{id}")]
		public IActionResult Edit(int id, [FromBody] Brand brand)
		{
			var existingBrand = _context.Brands.Find(id);
			if (existingBrand == null)
			{
				return NotFound(new { message = "Brand không tồn tại" });
			}

			existingBrand.Name = brand.Name;
			// Các thuộc tính khác nếu cần cập nhật

			_context.SaveChanges();
			return Ok(new { message = "Brand đã được cập nhật thành công!" });
		}


		[HttpDelete("Delete/{id}")]
        public IActionResult Delete(int id)
        {
            var brand = _context.Brands.Find(id);
            if (brand == null)
            {
                return NotFound(new { message = "Không tìm thấy " });
            }

            _context.Brands.Remove(brand);
            _context.SaveChanges();

            return Ok(new { message = "Brand đã được xóa thành công" });
        }
    }
}
