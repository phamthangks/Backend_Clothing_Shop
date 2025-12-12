using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using test1.Models;

namespace test1.Service
{
    public class StatisticsPdfService
    {
        private readonly QlbanQuanAoContext _context;

        public StatisticsPdfService(QlbanQuanAoContext context)
        {
            _context = context;
        }

        public byte[] GenerateStatisticsPdf()
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // Lấy tất cả dữ liệu thống kê
            var totalRevenue = (decimal)(_context.OrderDetails.Sum(od => od.TotalMoney) ?? 0);
            var totalOrders = _context.Orders.Count();
            var averageOrderValue = totalOrders > 0 ? (decimal)(totalRevenue / totalOrders) : (decimal)0;
            
            var averageItemsData = _context.OrderDetails
                .GroupBy(od => od.OrderId)
                .Select(g => new { OrderId = g.Key, TotalItems = g.Sum(od => od.NumberOfProducts) })
                .ToList();
            var averageItemsPerOrder = averageItemsData.Count > 0 
                ? (decimal)averageItemsData.Average(o => (double)(o.TotalItems ?? 0)) 
                : (decimal)0;

            var totalProducts = _context.Products.Count();
            var totalUsers = _context.Users.Count();

            // Top 5 sản phẩm bán chạy
            var topProducts = (
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
                    CategoryName = c != null ? c.Name : "Không xác định",
                    BrandName = b != null ? b.Name : "Không xác định"
                }
                into g
                select new TopProductInfo
                {
                    ProductName = g.Key.Name ?? "N/A",
                    Category = g.Key.CategoryName,
                    Brand = g.Key.BrandName,
                    TotalSold = g.Sum(x => x.NumberOfProducts) ?? 0,
                    Revenue = (decimal)(g.Sum(x => x.TotalMoney) ?? 0)
                }
            )
            .OrderByDescending(x => x.TotalSold)
            .Take(5)
            .ToList();

            // Đơn hàng theo trạng thái
            var ordersByStatus = _context.Orders
                .Where(o => o.Status != null)
                .GroupBy(o => o.Status)
                .Select(g => new StatusInfo
                {
                    Status = g.Key ?? "Không xác định",
                    Count = g.Count()
                })
                .ToList();

            // Đơn hàng theo phương thức thanh toán
            var ordersByPayment = _context.Orders
                .Where(o => o.PaymentMethod != null)
                .GroupBy(o => o.PaymentMethod)
                .Select(g => new MethodInfo
                {
                    Method = g.Key ?? "Không xác định",
                    Count = g.Count()
                })
                .ToList();

            // Đơn hàng theo phương thức vận chuyển
            var ordersByShipping = _context.Orders
                .Where(o => o.ShippingMethod != null)
                .GroupBy(o => o.ShippingMethod)
                .Select(g => new MethodInfo
                {
                    Method = g.Key ?? "Không xác định",
                    Count = g.Count()
                })
                .ToList();

            // Đơn hàng theo danh mục
            var ordersByCategory = (
                from od in _context.OrderDetails
                join p in _context.Products on od.ProductId equals p.Id
                join c in _context.Categories on p.CategoryId equals c.Id into cat
                from c in cat.DefaultIfEmpty()
                group od by new
                {
                    CategoryId = c != null ? (int?)c.Id : null,
                    CategoryName = c != null ? c.Name : "Unknown"
                }
                into g
                select new CategoryInfo
                {
                    CategoryName = g.Key.CategoryName,
                    OrderCount = g.Select(x => x.OrderId).Distinct().Count()
                }
            )
            .OrderBy(x => x.CategoryName)
            .ToList();

            // Đơn hàng theo thương hiệu
            var ordersByBrand = (
                from od in _context.OrderDetails
                join p in _context.Products on od.ProductId equals p.Id
                join b in _context.Brands on p.BrandId equals b.Id into br
                from b in br.DefaultIfEmpty()
                group od by new
                {
                    BrandId = b != null ? (int?)b.Id : null,
                    BrandName = b != null ? b.Name : "Unknown"
                }
                into g
                select new BrandInfo
                {
                    BrandName = g.Key.BrandName,
                    OrderCount = g.Select(x => x.OrderId).Distinct().Count()
                }
            )
            .OrderBy(x => x.BrandName)
            .ToList();

            // Thống kê tháng hiện tại
            var now = DateTime.Now;
            var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            var totalOrdersThisMonth = _context.Orders
                .Count(o => o.OrderDate.HasValue &&
                           o.OrderDate.Value >= firstDayOfMonth &&
                           o.OrderDate.Value <= lastDayOfMonth);

            var revenueThisMonth = (decimal)_context.Orders
                .Where(o => o.OrderDate.HasValue &&
                           o.OrderDate.Value >= firstDayOfMonth &&
                           o.OrderDate.Value <= lastDayOfMonth &&
                           o.Status != "Cancelled" &&
                           o.Status != "Đã hủy")
                .Sum(o => o.TotalMoney ?? 0);

            // Tạo PDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Header
                    page.Header()
                        .Column(column =>
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("BÁO CÁO THỐNG KÊ")
                                        .FontSize(24)
                                        .Bold()
                                        .FontColor(Colors.Blue.Darken3);
                                    col.Item().Text("Hệ thống quản lý bán hàng")
                                        .FontSize(12)
                                        .FontColor(Colors.Grey.Darken1);
                                });
                                row.ConstantItem(80).AlignRight().Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Darken1);
                            });
                            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                        });

                    // Content
                    page.Content()
                        .PaddingVertical(10)
                        .Column(column =>
                        {
                            // Tổng quan
                            column.Item().Element(c => CreateSummarySection(c, totalRevenue, totalOrders, 
                                averageOrderValue, averageItemsPerOrder, totalProducts, totalUsers, 
                                totalOrdersThisMonth, revenueThisMonth));

                            column.Item().PaddingTop(15);

                            // Top sản phẩm bán chạy
                            column.Item().Element(c => CreateTopProductsSection(c, topProducts));

                            column.Item().PaddingTop(15);

                            // Đơn hàng theo trạng thái
                            column.Item().Element(c => CreateOrdersByStatusSection(c, ordersByStatus));

                            column.Item().PaddingTop(15);

                            // Đơn hàng theo phương thức thanh toán
                            column.Item().Element(c => CreateOrdersByPaymentSection(c, ordersByPayment));

                            column.Item().PaddingTop(15);

                            // Đơn hàng theo phương thức vận chuyển
                            column.Item().Element(c => CreateOrdersByShippingSection(c, ordersByShipping));

                            column.Item().PaddingTop(15);

                            // Đơn hàng theo danh mục
                            column.Item().Element(c => CreateOrdersByCategorySection(c, ordersByCategory));

                            column.Item().PaddingTop(15);

                            // Đơn hàng theo thương hiệu
                            column.Item().Element(c => CreateOrdersByBrandSection(c, ordersByBrand));
                        });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1))
                        .Text(x =>
                        {
                            x.Span("Trang ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            });

            return document.GeneratePdf();
        }

        private void CreateSummarySection(IContainer container, decimal totalRevenue, int totalOrders,
            decimal averageOrderValue, decimal averageItemsPerOrder, int totalProducts, int totalUsers,
            int totalOrdersThisMonth, decimal revenueThisMonth)
        {
            container
                .Background(Colors.Blue.Lighten5)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("TỔNG QUAN THỐNG KÊ")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Blue.Darken3);

                    // Hàng 1
                    column.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("Tổng Doanh Thu")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(FormatCurrency(totalRevenue))
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Blue.Darken3);
                        });
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("Tổng Đơn Hàng")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(totalOrders.ToString("N0"))
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Green.Darken3);
                        });
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("Giá Trị TB/Đơn")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(FormatCurrency(averageOrderValue))
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Orange.Darken3);
                        });
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("SP TB/Đơn")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(Math.Round(averageItemsPerOrder, 2).ToString())
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Purple.Darken3);
                        });
                    });

                    // Hàng 2
                    column.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("Tổng Sản Phẩm")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(totalProducts.ToString("N0"))
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Blue.Darken3);
                        });
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("Tổng Người Dùng")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(totalUsers.ToString("N0"))
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Green.Darken3);
                        });
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("Đơn Hàng Tháng Này")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(totalOrdersThisMonth.ToString("N0"))
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Orange.Darken3);
                        });
                        row.RelativeItem().Background(Colors.White).Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Column(col =>
                        {
                            col.Item().Text("Doanh Thu Tháng Này")
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken2);
                            col.Item().PaddingTop(5).Text(FormatCurrency(revenueThisMonth))
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.Purple.Darken3);
                        });
                    });
                });
        }

        private void CreateTopProductsSection(IContainer container, List<TopProductInfo> topProducts)
        {
            container
                .Background(Colors.Green.Lighten5)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("TOP 5 SẢN PHẨM BÁN CHẠY")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Green.Darken3);

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(0.5f);
                            columns.RelativeColumn(2f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("STT").Bold();
                            header.Cell().Element(CellStyle).Text("Tên Sản Phẩm").Bold();
                            header.Cell().Element(CellStyle).Text("Danh Mục").Bold();
                            header.Cell().Element(CellStyle).Text("Thương Hiệu").Bold();
                            header.Cell().Element(CellStyle).Text("Đã Bán").Bold();
                        });

                        // Rows
                        int index = 1;
                        foreach (var product in topProducts)
                        {
                            table.Cell().Element(CellStyle).Text(index++.ToString());
                            table.Cell().Element(CellStyle).Text(product.ProductName ?? "N/A");
                            table.Cell().Element(CellStyle).Text(product.Category ?? "N/A");
                            table.Cell().Element(CellStyle).Text(product.Brand ?? "N/A");
                            table.Cell().Element(CellStyle).Text(product.TotalSold.ToString() + " SP");
                        }
                    });
                });
        }

        private void CreateOrdersByStatusSection(IContainer container, List<StatusInfo> ordersByStatus)
        {
            container
                .Background(Colors.Orange.Lighten5)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("ĐƠN HÀNG THEO TRẠNG THÁI")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Orange.Darken3);

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Trạng Thái").Bold();
                            header.Cell().Element(CellStyle).Text("Số Lượng").Bold();
                        });

                        foreach (var item in ordersByStatus)
                        {
                            table.Cell().Element(CellStyle).Text(item.Status ?? "N/A");
                            table.Cell().Element(CellStyle).Text(item.Count.ToString("N0"));
                        }
                    });
                });
        }

        private void CreateOrdersByPaymentSection(IContainer container, List<MethodInfo> ordersByPayment)
        {
            container
                .Background(Colors.Purple.Lighten5)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("ĐƠN HÀNG THEO PHƯƠNG THỨC THANH TOÁN")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Purple.Darken3);

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Phương Thức").Bold();
                            header.Cell().Element(CellStyle).Text("Số Lượng").Bold();
                        });

                        foreach (var item in ordersByPayment)
                        {
                            table.Cell().Element(CellStyle).Text(item.Method ?? "N/A");
                            table.Cell().Element(CellStyle).Text(item.Count.ToString("N0"));
                        }
                    });
                });
        }

        private void CreateOrdersByShippingSection(IContainer container, List<MethodInfo> ordersByShipping)
        {
            container
                .Background(Colors.Teal.Lighten5)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("ĐƠN HÀNG THEO PHƯƠNG THỨC VẬN CHUYỂN")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Teal.Darken3);

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Phương Thức").Bold();
                            header.Cell().Element(CellStyle).Text("Số Lượng").Bold();
                        });

                        foreach (var item in ordersByShipping)
                        {
                            table.Cell().Element(CellStyle).Text(item.Method ?? "N/A");
                            table.Cell().Element(CellStyle).Text(item.Count.ToString("N0"));
                        }
                    });
                });
        }

        private void CreateOrdersByCategorySection(IContainer container, List<CategoryInfo> ordersByCategory)
        {
            container
                .Background(Colors.Blue.Lighten5)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("ĐƠN HÀNG THEO DANH MỤC")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Blue.Darken3);

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Danh Mục").Bold();
                            header.Cell().Element(CellStyle).Text("Số Đơn").Bold();
                        });

                        foreach (var item in ordersByCategory)
                        {
                            table.Cell().Element(CellStyle).Text(item.CategoryName ?? "N/A");
                            table.Cell().Element(CellStyle).Text(item.OrderCount.ToString("N0"));
                        }
                    });
                });
        }

        private void CreateOrdersByBrandSection(IContainer container, List<BrandInfo> ordersByBrand)
        {
            container
                .Background(Colors.Red.Lighten5)
                .Padding(15)
                .Column(column =>
                {
                    column.Item().Text("ĐƠN HÀNG THEO THƯƠNG HIỆU")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Red.Darken3);

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Thương Hiệu").Bold();
                            header.Cell().Element(CellStyle).Text("Số Đơn").Bold();
                        });

                        foreach (var item in ordersByBrand)
                        {
                            table.Cell().Element(CellStyle).Text(item.BrandName ?? "N/A");
                            table.Cell().Element(CellStyle).Text(item.OrderCount.ToString("N0"));
                        }
                    });
                });
        }

        private IContainer CellStyle(IContainer container)
        {
            return container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(8)
                .Background(Colors.White);
        }

        private static string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0") + " VNĐ";
        }
    }

    // Helper classes để tránh lỗi dynamic
    internal class TopProductInfo
    {
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public string? Brand { get; set; }
        public int TotalSold { get; set; }
        public decimal Revenue { get; set; }
    }

    internal class StatusInfo
    {
        public string? Status { get; set; }
        public int Count { get; set; }
    }

    internal class MethodInfo
    {
        public string? Method { get; set; }
        public int Count { get; set; }
    }

    internal class CategoryInfo
    {
        public string? CategoryName { get; set; }
        public int OrderCount { get; set; }
    }

    internal class BrandInfo
    {
        public string? BrandName { get; set; }
        public int OrderCount { get; set; }
    }
}
