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

		[HttpGet("variants")]
		public IActionResult GetProductVariants()
		{
			var variants = _db.ProductVariants
				.Include(pv => pv.Product)
				.AsNoTracking()
				.ToList();
			return Ok(variants);
		}

		/// <summary>
		/// Lấy 30 sản phẩm bán chạy nhất (mua nhiều nhất)
		/// </summary>
		[HttpGet("top-selling")]
		public IActionResult GetTopSellingProducts([FromQuery] int limit = 30)
		{
			try
			{
				// Lấy danh sách sản phẩm đã được mua (chỉ tính đơn hàng đã hoàn thành),
				// nhóm theo ProductId và tính tổng số lượng đã bán
				var topSellingProductIds = (
					from od in _db.OrderDetails
					join o in _db.Orders on od.OrderId equals o.Id
					where od.NumberOfProducts.HasValue && od.NumberOfProducts.Value > 0
						&& od.ProductId.HasValue
						&& o.Active == false 
						&& o.Status == "completed"
					group od by od.ProductId.Value into g
					select new
					{
						ProductId = g.Key,
						TotalSold = g.Sum(od => od.NumberOfProducts.Value)
					}
				)
				.OrderByDescending(x => x.TotalSold)
				.Take(limit)
				.Select(x => x.ProductId)
				.ToList();

				// Lấy thông tin đầy đủ của các sản phẩm này
				var topProducts = _db.Products
					.Include(p => p.ProductImages)
					.Include(p => p.Category)
					.Include(p => p.Brand)
					.Where(p => topSellingProductIds.Contains(p.Id))
					.AsNoTracking()
					.ToList();

				// Sắp xếp lại theo thứ tự TotalSold (giữ nguyên thứ tự từ query trên)
				var orderedProducts = topSellingProductIds
					.Select(id => topProducts.FirstOrDefault(p => p.Id == id))
					.Where(p => p != null)
					.ToList();

				// Nếu có ít hơn limit sản phẩm đã được mua, thêm các sản phẩm chưa được mua để đủ 30
				if (orderedProducts.Count < limit)
				{
					var remainingCount = limit - orderedProducts.Count;
					var soldProductIds = orderedProducts.Select(p => p.Id).ToList();
					var additionalProducts = _db.Products
						.Include(p => p.ProductImages)
						.Include(p => p.Category)
						.Include(p => p.Brand)
						.Where(p => !soldProductIds.Contains(p.Id))
						.OrderByDescending(p => p.CreatedAt)
						.Take(remainingCount)
						.AsNoTracking()
						.ToList();

					orderedProducts.AddRange(additionalProducts);
				}

				return Ok(orderedProducts);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { message = "Lỗi khi lấy sản phẩm bán chạy", error = ex.Message });
			}
		}
	}
}
