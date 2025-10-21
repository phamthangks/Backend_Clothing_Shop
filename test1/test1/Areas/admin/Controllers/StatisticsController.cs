using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using test1.Models;

namespace test1.Areas.admin.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StatisticsController : ControllerBase
    {
        private readonly QlbanQuanAoContext _context;

        public StatisticsController(QlbanQuanAoContext context)
        {
            _context = context;
        }

        // Get total revenue
        [HttpGet("total-revenue")]
        public async Task<IActionResult> GetTotalRevenue()
        {
            var totalRevenue = await _context.OrderDetails.SumAsync(od => od.TotalMoney);
            var totalOrders = await _context.Orders.CountAsync();

            return Ok(new
            {
                TotalRevenue = totalRevenue,
                TotalOrders = totalOrders
            });
        }


        // GET /api/statistics/orders-by-day
        [HttpGet("orders-by-day")]
        public async Task<IActionResult> GetOrdersByDay()
        {
            // 1) Group by the DateTime.Date in the database
            var data = await _context.Orders
                .Where(o => o.OrderDate.HasValue)
                .GroupBy(o => o.OrderDate.Value.Date)
                .Select(g => new
                {
                    Date = g.Key,          // DateTime
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();           // bring into memory

            // 2) Now format the date as a string
            var result = data
                .Select(x => new
                {
                    Date = x.Date.ToString("yyyy-MM-dd"),
                    x.Count
                });

            return Ok(result);
        }

        // Get orders count grouped by year/month (for every month in the DB)
        [HttpGet("orders-by-month")]
        public async Task<IActionResult> GetOrdersByMonth()
        {
            var result = await _context.Orders
                .Where(o => o.OrderDate.HasValue)
                .GroupBy(o => new {
                    Year = o.OrderDate.Value.Year,
                    Month = o.OrderDate.Value.Month
                })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            return Ok(result);
        }



        // Get orders by status
        [HttpGet("orders-by-status")]
        public async Task<IActionResult> GetOrdersByStatus()
        {
            var result = await _context.Orders
                .Where(o => o.Status != null) // ignore null statuses
                .GroupBy(o => o.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            return Ok(result);
        }


        // Get average order value
        [HttpGet("average-order-value")]
        public async Task<IActionResult> GetAverageOrderValue()
        {
            var average = await _context.Orders.AverageAsync(o => o.TotalMoney);
            return Ok(average);
        }

        // Get average items per order
        [HttpGet("average-items-per-order")]
        public async Task<IActionResult> GetAverageItemsPerOrder()
        {
            var groupedOrders = await _context.OrderDetails
                .GroupBy(od => od.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    TotalItems = g.Sum(od => od.NumberOfProducts)
                })
                .ToListAsync();

            var totalOrders = groupedOrders.Count;
            var totalItems = groupedOrders.Sum(o => o.TotalItems);
            var average = totalOrders > 0 ? (double)totalItems / totalOrders : 0;

            return Ok(new
            {
                TotalOrders = totalOrders,
                TotalItems = totalItems,
                AverageItemsPerOrder = Math.Round(average, 2)
            });
        }


        // Get orders by shipping method (excluding null methods)
        [HttpGet("orders-by-shipping-method")]
        public async Task<IActionResult> GetOrdersByShippingMethod()
        {
            var result = await _context.Orders
                .Where(o => o.ShippingMethod != null) // Exclude null shipping methods
                .GroupBy(o => o.ShippingMethod) // Group by shipping method
                .Select(g => new { Method = g.Key, Count = g.Count() }) // Select method and count of orders
                .ToListAsync();

            return Ok(result);
        }


        // Get orders by payment method (excluding null payment methods)
        [HttpGet("orders-by-payment-method")]
        public async Task<IActionResult> GetOrdersByPaymentMethod()
        {
            var result = await _context.Orders
                .Where(o => o.PaymentMethod != null) // Exclude null payment methods
                .GroupBy(o => o.PaymentMethod) // Group by payment method
                .Select(g => new { Method = g.Key, Count = g.Count() }) // Select method and count of orders
                .ToListAsync();

            return Ok(result);
        }

        // GET api/statistics/orders-by-category
        [HttpGet("orders-by-category")]
        public async Task<IActionResult> GetOrdersByCategory()
        {
            var result = await (
                from od in _context.OrderDetails

                    // inner‐join to Products
                join p in _context.Products
                    on od.ProductId equals p.Id

                // left‐join to Categories
                join c in _context.Categories
                    on p.CategoryId equals c.Id into cat
                from c in cat.DefaultIfEmpty()

                group od by new
                {
                    CategoryId = c != null ? (int?)c.Id : null,
                    CategoryName = c != null ? c.Name : "Unknown"
                }
                into g
                select new
                {
                    CategoryId = g.Key.CategoryId,
                    CategoryName = g.Key.CategoryName,
                    OrderCount = g
                        .Select(x => x.OrderId)
                        .Distinct()
                        .Count()
                }
            )
            .OrderBy(x => x.CategoryName)
            .ToListAsync();

            return Ok(result);
        }


        // GET api/statistics/orders-by-brand
        [HttpGet("orders-by-brand")]
        public async Task<IActionResult> GetOrdersByBrand()
        {
            var result = await (
                from od in _context.OrderDetails

                    // inner‐join to Products
                join p in _context.Products
                    on od.ProductId equals p.Id

                // left‐join to Brands
                join b in _context.Brands
                    on p.BrandId equals b.Id into br
                from b in br.DefaultIfEmpty()

                group od by new
                {
                    BrandId = b != null ? (int?)b.Id : null,
                    BrandName = b != null ? b.Name : "Unknown"
                }
                into g
                select new
                {
                    BrandId = g.Key.BrandId,
                    BrandName = g.Key.BrandName,
                    OrderCount = g
                        .Select(x => x.OrderId)
                        .Distinct()
                        .Count()
                }
            )
            .OrderBy(x => x.BrandName)
            .ToListAsync();

            return Ok(result);
        }





        [HttpGet("top5-best-selling-products")]
        public async Task<IActionResult> GetTop5BestSellingProducts()
        {
            //var result = await _context.OrderDetails
            //    .Join(_context.Products,
            //          od => od.ProductId,
            //          p => p.Id,
            //          (od, p) => new { od.NumberOfProducts, Product = p })
            //    .Join(_context.Categories,
            //          x => x.Product.CategoryId,
            //          c => c.Id,
            //          (x, c) => new { x.NumberOfProducts, x.Product, Category = c })
            //    .Join(_context.Brands,
            //          x => x.Product.BrandId,
            //          b => b.Id,
            //          (x, b) => new
            //          {
            //              x.NumberOfProducts,
            //              x.Product.Id,
            //              x.Product.Name,
            //              x.Product.Price,
            //              x.Product.Description,
            //              CategoryName = x.Category.Name,
            //              BrandName = b.Name
            //          })
            //    .GroupBy(x => new { x.Id, x.Name, x.Price, x.Description, x.CategoryName, x.BrandName })
            //    .Select(g => new
            //    {
            //        ProductId = g.Key.Id,
            //        ProductName = g.Key.Name,
            //        Price = g.Key.Price,
            //        Description = g.Key.Description,
            //        Category = g.Key.CategoryName,
            //        Brand = g.Key.BrandName,
            //        QuantitySold = g.Sum(x => x.NumberOfProducts)
            //    })
            //    .OrderByDescending(x => x.QuantitySold)
            //    .Take(5)
            //    .ToListAsync();

            //return Ok(result);
            var result = await (
        from od in _context.OrderDetails
        join p in _context.Products on od.ProductId equals p.Id
        // optional: include category/brand names
        join c in _context.Categories on p.CategoryId equals c.Id into cat
        from c in cat.DefaultIfEmpty()
        join b in _context.Brands on p.BrandId equals b.Id into br
        from b in br.DefaultIfEmpty()
        group od by new
        {
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            CategoryName = c != null ? c.Name : "Unknown",
            BrandName = b != null ? b.Name : "Unknown"
        }
        into g
        select new
        {
            ProductId = g.Key.Id,
            ProductName = g.Key.Name,
            Price = g.Key.Price,
            Description = g.Key.Description,
            Category = g.Key.CategoryName,
            Brand = g.Key.BrandName,
            QuantityOrdered = g.Sum(x => x.NumberOfProducts)
        }
    )
    .OrderByDescending(x => x.QuantityOrdered)
    .ToListAsync();

            return Ok(result);
        }


         [HttpGet("average-orders-today")]
        public async Task<IActionResult> GetAverageOrdersToday()
        {
            var today = DateTime.Today;

            var ordersToday = await _context.Orders
                .Where(o => o.OrderDate.HasValue && o.OrderDate.Value.Date == today)
                .ToListAsync();

            var totalOrders = ordersToday.Count;
            var averagePerUser = totalOrders > 0
                ? ordersToday.GroupBy(o => o.UserId).Average(g => g.Count())
                : 0;

            return Ok(new
            {
                TotalOrders = totalOrders,
                AverageOrdersPerUser = Math.Round(averagePerUser, 2)
            });
        }

        // GET /api/statistics/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var now = DateTime.Now;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            // Tổng đơn hàng tháng này
            var totalOrdersThisMonth = await _context.Orders
                .Where(o => o.OrderDate.HasValue && 
                           o.OrderDate.Value >= firstDayOfMonth && 
                           o.OrderDate.Value <= lastDayOfMonth)
                .CountAsync();

            // Doanh thu tháng này
            var revenueThisMonth = await _context.Orders
                .Where(o => o.OrderDate.HasValue && 
                           o.OrderDate.Value >= firstDayOfMonth && 
                           o.OrderDate.Value <= lastDayOfMonth &&
                           o.Status != "Cancelled" &&
                           o.Status != "Đã hủy")
                .SumAsync(o => o.TotalMoney ?? 0);

            // Tổng số sản phẩm
            var totalProducts = await _context.Products.CountAsync();

            // Tổng số người dùng
            var totalUsers = await _context.Users.CountAsync();

            // Các hoạt động gần đây (10 đơn hàng mới nhất)
            var recentActivities = await _context.Orders
                .OrderByDescending(o => o.OrderDate)
                .Take(10)
                .Select(o => new
                {
                    o.Id,
                    o.Fullname,
                    o.OrderDate,
                    o.Status,
                    o.TotalMoney,
                    o.PaymentMethod
                })
                .ToListAsync();

            // Thống kê đơn hàng theo trạng thái tháng này
            var orderStatusStats = await _context.Orders
                .Where(o => o.OrderDate.HasValue && 
                           o.OrderDate.Value >= firstDayOfMonth && 
                           o.OrderDate.Value <= lastDayOfMonth)
                .GroupBy(o => o.Status)
                .Select(g => new
                {
                    Status = g.Key ?? "Không xác định",
                    Count = g.Count()
                })
                .ToListAsync();

            return Ok(new
            {
                TotalOrdersThisMonth = totalOrdersThisMonth,
                RevenueThisMonth = Math.Round(revenueThisMonth, 2),
                TotalProducts = totalProducts,
                TotalUsers = totalUsers,
                RecentActivities = recentActivities,
                OrderStatusStats = orderStatusStats
            });
        }

        // GET /api/statistics/monthly-revenue-chart
        [HttpGet("monthly-revenue-chart")]
        public async Task<IActionResult> GetMonthlyRevenueChart()
        {
            var now = DateTime.Now;
            var last6Months = new List<object>();

            for (int i = 5; i >= 0; i--)
            {
                var month = now.AddMonths(-i);
                var firstDay = new DateTime(month.Year, month.Month, 1);
                var lastDay = firstDay.AddMonths(1).AddDays(-1);

                var revenue = await _context.Orders
                    .Where(o => o.OrderDate.HasValue && 
                               o.OrderDate.Value >= firstDay && 
                               o.OrderDate.Value <= lastDay &&
                               o.Status != "Cancelled" &&
                               o.Status != "Đã hủy")
                    .SumAsync(o => o.TotalMoney ?? 0);

                var orderCount = await _context.Orders
                    .Where(o => o.OrderDate.HasValue && 
                               o.OrderDate.Value >= firstDay && 
                               o.OrderDate.Value <= lastDay)
                    .CountAsync();

                last6Months.Add(new
                {
                    Month = month.ToString("MM/yyyy"),
                    Revenue = Math.Round(revenue, 2),
                    OrderCount = orderCount
                });
            }

            return Ok(last6Months);
        }

        // GET /api/statistics/top-selling-products
        [HttpGet("top-selling-products")]
        public async Task<IActionResult> GetTopSellingProducts([FromQuery] int limit = 5)
        {
            var result = await (
                from od in _context.OrderDetails
                join p in _context.Products on od.ProductId equals p.Id
                join c in _context.Categories on p.CategoryId equals c.Id into cat
                from c in cat.DefaultIfEmpty()
                join b in _context.Brands on p.BrandId equals b.Id into br
                from b in br.DefaultIfEmpty()
                group od by new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Image,
                    CategoryName = c != null ? c.Name : "Không xác định",
                    BrandName = b != null ? b.Name : "Không xác định"
                }
                into g
                select new
                {
                    ProductId = g.Key.Id,
                    ProductName = g.Key.Name,
                    Price = g.Key.Price,
                    Image = g.Key.Image,
                    Category = g.Key.CategoryName,
                    Brand = g.Key.BrandName,
                    TotalSold = g.Sum(x => x.NumberOfProducts),
                    Revenue = g.Sum(x => x.TotalMoney)
                }
            )
            .OrderByDescending(x => x.TotalSold)
            .Take(limit)
            .ToListAsync();

            return Ok(result);
        }

        // GET /api/statistics/user-growth
        [HttpGet("user-growth")]
        public async Task<IActionResult> GetUserGrowth()
        {
            var now = DateTime.Now;
            var last6Months = new List<object>();

            for (int i = 5; i >= 0; i--)
            {
                var month = now.AddMonths(-i);
                var firstDay = new DateTime(month.Year, month.Month, 1);
                var lastDay = firstDay.AddMonths(1).AddDays(-1);

                var newUsers = await _context.Users
                    .Where(u => u.CreatedAt.HasValue && 
                               u.CreatedAt.Value >= firstDay && 
                               u.CreatedAt.Value <= lastDay)
                    .CountAsync();

                var totalUsers = await _context.Users
                    .Where(u => u.CreatedAt.HasValue && 
                               u.CreatedAt.Value <= lastDay)
                    .CountAsync();

                last6Months.Add(new
                {
                    Month = month.ToString("MM/yyyy"),
                    NewUsers = newUsers,
                    TotalUsers = totalUsers
                });
            }

            return Ok(last6Months);
        }


    }
}
