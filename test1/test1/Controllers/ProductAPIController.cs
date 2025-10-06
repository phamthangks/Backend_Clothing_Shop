using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test1.Models;
using test1.ViewModels;
using System.Linq;
using System.Collections.Generic;
using X.PagedList.Extensions;

namespace Test.Controllers
{
	[Route("api/products")]
	[ApiController]
	public class ProductApiController : ControllerBase
	{
		private readonly QlbanQuanAoContext _db;

		public ProductApiController(QlbanQuanAoContext db)
		{
			_db = db;
		}

		[HttpGet("all")]
		public IActionResult GetAllProducts()
		{
			var lstSanPham = _db.Products.AsNoTracking().ToList();
			return Ok(lstSanPham);
		}
		[HttpGet("paged")]
		public IActionResult GetProducts(int? page, int? pageSize)
		{
			int defaultPageSize = 6;
			int pageNumber = (page.HasValue && page.Value > 0) ? page.Value : 1;
			int effectivePageSize = (pageSize.HasValue && pageSize.Value > 0) ? pageSize.Value : defaultPageSize;

			var productsQuery = _db.Products.AsNoTracking();
			var pagedProducts = productsQuery.ToPagedList(pageNumber, effectivePageSize);

			var response = new
			{
				items = pagedProducts.ToList(),
				total = pagedProducts.TotalItemCount
			};

			return Ok(response);
		}


		[HttpGet("{id}")]
		public IActionResult GetProductDetail(int id)
		{
			var sanPham = _db.Products.SingleOrDefault(x => x.Id == id);
			if (sanPham == null)
				return NotFound();

			var anhSP = _db.ProductImages.Where(x => x.ProductId == id).ToList();
			var productDetail = new ProductDetailViewModel
			{
				Product = sanPham,
				anhSps = anhSP
			};
			return Ok(productDetail);
		}
	}
}
