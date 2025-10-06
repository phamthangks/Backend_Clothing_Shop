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
		private readonly QlbanQuanAoContext _context = new QlbanQuanAoContext();
		[HttpGet("all")]
		public IActionResult GetAllProducts()
		{
			var lstSanPham = _context.Products.AsNoTracking().ToList();
			return Ok(lstSanPham);
		}
		[HttpGet("paged")]
		public IActionResult GetProducts(int? page, int? pageSize)
		{
			int defaultPageSize = 6;
			int pageNumber = (page.HasValue && page.Value > 0) ? page.Value : 1;
			int effectivePageSize = (pageSize.HasValue && pageSize.Value > 0) ? pageSize.Value : defaultPageSize;

			var productsQuery = _context.Products.AsNoTracking();
			var pagedProducts = productsQuery.ToPagedList(pageNumber, effectivePageSize);

			var response = new
			{
				items = pagedProducts.ToList(),
				total = pagedProducts.TotalItemCount
			};

			return Ok(response);
		}


		[HttpPost("Create")]
		public IActionResult Create([FromBody] Product product)
		{
			if (ModelState.IsValid)
			{
				_context.Products.Add(product);
				_context.SaveChanges();
				return Ok(new { message = "Sản phẩm đã được tạo thành công", productId = product.Id });
			}
			return BadRequest(ModelState);
		}
		[HttpPut("Edit/{id}")]
		public IActionResult Edit(int id, [FromBody] Product product)
		{
			if (id != product.Id)
				return BadRequest(new { message = "ID không khớp với Product.Id" });

			if (!ModelState.IsValid)
				return BadRequest(ModelState);

			var existingProduct = _context.Products.Find(id);
			if (existingProduct == null)
				return NotFound(new { message = $"Không tìm thấy sản phẩm với ID {id}" });

			// Cập nhật các thuộc tính cần thiết
			existingProduct.Name = product.Name;
			existingProduct.Price = product.Price;
			existingProduct.Description = product.Description;
			existingProduct.CategoryId = product.CategoryId;
			existingProduct.BrandId = product.BrandId;
			// Thêm các thuộc tính khác nếu có

			_context.SaveChanges();

			return Ok(new { success = true, message = "Sản phẩm đã được cập nhật thành công!" });
		}


		[HttpDelete("Delete/{id}")]
		public IActionResult Delete(int id)
		{
			var product = _context.Products.Find(id);
			if (product == null)
			{
				return NotFound(new { message = "Không tìm thấy sản phẩm" });
			}

			_context.Products.Remove(product);
			_context.SaveChanges();

			return Ok(new { message = "Sản phẩm đã được xóa thành công" });
		}
	}
}
