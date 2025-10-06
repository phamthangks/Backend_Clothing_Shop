using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using test1.Models;
using System.Collections.Generic;
using System.Linq;
using test1.Models;

namespace test1.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class LoaiSPController : ControllerBase
	{
		// Nên dùng Dependency Injection để lấy QlbanQuanAoContext
		private readonly QlbanQuanAoContext _context = new QlbanQuanAoContext();

		/// <summary>
		/// Lấy danh sách sản phẩm theo danh mục (Category).
		/// Ví dụ: GET api/LoaiSP/category/5
		/// </summary>
		/// <param name="MaLoai">Mã danh mục</param>
		/// <returns>Danh sách sản phẩm thuộc danh mục</returns>
		[HttpGet("category/{MaLoai}")]
		public IEnumerable<Product> GetProductsByCategory(int MaLoai)
		{
			var productsByCategory = (from p in _context.Products
									  where p.CategoryId == MaLoai
									  select new Product
									  {
										  Id = p.Id,
										  CategoryId = p.CategoryId,
										  BrandId = p.BrandId,
										  Name = p.Name,
										  Image = p.Image,
										  Price = p.Price
									  }).ToList();

			return productsByCategory;
		}

		/// <summary>
		/// Lấy danh sách sản phẩm theo thương hiệu (Brand).
		/// Ví dụ: GET api/LoaiSP/brand/3
		/// </summary>
		/// <param name="MaLoai">Mã thương hiệu</param>
		/// <returns>Danh sách sản phẩm của thương hiệu</returns>
		[HttpGet("brand/{MaLoai}")]
		public IEnumerable<Product> GetProductsByBrand(int MaLoai)
		{
			var productsByBrand = (from p in _context.Products
								   where p.BrandId == MaLoai
								   select new Product
								   {
									   Id = p.Id,
									   CategoryId = p.CategoryId,
									   BrandId = p.BrandId,
									   Name = p.Name,
									   Image = p.Image,
									   Price = p.Price
								   }).ToList();

			return productsByBrand;
		}

		/// <summary>
		/// Lấy danh sách sản phẩm theo khoảng giá.
		/// Ví dụ: GET api/LoaiSP/price?minPrice=100&maxPrice=500
		/// </summary>
		/// <param name="minPrice">Giá nhỏ nhất</param>
		/// <param name="maxPrice">Giá lớn nhất</param>
		/// <returns>Danh sách sản phẩm trong khoảng giá</returns>
		[HttpGet("price")]
		public IEnumerable<Product> GetProductsByPrice([FromQuery] double minPrice, [FromQuery] double maxPrice)
		{
			var productsInRange = (from p in _context.Products
								   where p.Price >= minPrice && p.Price <= maxPrice
								   select new Product
								   {
									   Id = p.Id,
									   CategoryId = p.CategoryId,
									   BrandId = p.BrandId,
									   Name = p.Name,
									   Image = p.Image,
									   Price = p.Price
								   }).ToList();

			return productsInRange;
		}

		/// <summary>
		/// Tìm kiếm sản phẩm theo tên.
		/// Ví dụ: GET api/LoaiSP/search?name=áo
		/// </summary>
		/// <param name="name">Chuỗi tìm kiếm</param>
		/// <returns>Danh sách sản phẩm tìm được</returns>
		[HttpGet("search")]
		public IActionResult GetProductsByName([FromQuery] string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return BadRequest(new { message = "Tham số tìm kiếm không được để trống." });
			}

			var productsByName = (from p in _context.Products
								  where p.Name.Contains(name)
								  select new Product
								  {
									  Id = p.Id,
									  CategoryId = p.CategoryId,
									  BrandId = p.BrandId,
									  Name = p.Name,
									  Image = p.Image,
									  Price = p.Price
								  }).ToList();

			return Ok(productsByName);
		}
		// Endpoint lấy danh mục
		[HttpGet("categories")]
		public ActionResult<IEnumerable<Category>> GetCategories()
		{
			var categories = _context.Categories.ToList();
			return Ok(categories);
		}

		// Endpoint lấy thương hiệu
		[HttpGet("brands")]
		public ActionResult<IEnumerable<Brand>> GetBrands()
		{
			var brands = _context.Brands.ToList();
			return Ok(brands);
		}
	}
}
