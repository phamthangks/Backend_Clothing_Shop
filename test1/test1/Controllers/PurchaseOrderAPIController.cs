using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using test1.Models;

namespace test1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PurchaseOrderAPIController : ControllerBase
    {
        private readonly QlbanQuanAoContext _context;

        public PurchaseOrderAPIController(QlbanQuanAoContext context)
        {
            _context = context;
        }

        // GET: api/PurchaseOrderAPI
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PurchaseOrder>>> GetPurchaseOrders()
        {
            var purchaseOrders = await _context.PurchaseOrders
                .Include(po => po.User)
                .Include(po => po.PurchaseOrderDetails)
                    .ThenInclude(pod => pod.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .OrderByDescending(po => po.CreatedAt).AsNoTracking()
                .ToListAsync();

            return Ok(purchaseOrders);
        }

        // GET: api/PurchaseOrderAPI/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PurchaseOrder>> GetPurchaseOrder(int id)
        {
            var purchaseOrder = await _context.PurchaseOrders
                .Include(po => po.User)
                .Include(po => po.PurchaseOrderDetails)
                    .ThenInclude(pod => pod.ProductVariant)
                        .ThenInclude(pv => pv.Product)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (purchaseOrder == null)
            {
                return NotFound();
            }

            return Ok(purchaseOrder);
        }

        // POST: api/PurchaseOrderAPI
        [HttpPost]
        public async Task<ActionResult<PurchaseOrder>> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request)
        {
            try
            {
                // Tạo PurchaseOrder mới
                var purchaseOrder = new PurchaseOrder
                {
                    SupplierName = request.SupplierName,
                    Note = request.Note,
                    CreatedAt = DateTime.Now,
                    UserId = GetCurrentUserId() // Lấy ID của user hiện tại
                };

                _context.PurchaseOrders.Add(purchaseOrder);
                await _context.SaveChangesAsync();

                // Tạo PurchaseOrderDetails
                foreach (var detailRequest in request.PurchaseOrderDetails)
                {
                    var purchaseOrderDetail = new PurchaseOrderDetail
                    {
                        PurchaseOrderId = purchaseOrder.Id,
                        ProductVariantId = detailRequest.ProductVariantId,
                        Quantity = detailRequest.Quantity,
                        ImportPrice = detailRequest.ImportPrice,
                        TotalPrice = detailRequest.Quantity * detailRequest.ImportPrice
                    };

                    _context.PurchaseOrderDetails.Add(purchaseOrderDetail);

                    // Cập nhật stock quantity của ProductVariant
                    var productVariant = await _context.ProductVariants
                        .FirstOrDefaultAsync(pv => pv.Id == detailRequest.ProductVariantId);
                    
                    if (productVariant != null)
                    {
                        productVariant.StockQuantity += detailRequest.Quantity;
                        _context.ProductVariants.Update(productVariant);
                    }
                }

                await _context.SaveChangesAsync();

                // Load lại PurchaseOrder với đầy đủ thông tin
                var createdPurchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.User)
                    .Include(po => po.PurchaseOrderDetails)
                        .ThenInclude(pod => pod.ProductVariant)
                            .ThenInclude(pv => pv.Product)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrder.Id);

                return CreatedAtAction(nameof(GetPurchaseOrder), new { id = purchaseOrder.Id }, createdPurchaseOrder);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi tạo đơn nhập hàng: " + ex.Message });
            }
        }

        // PUT: api/PurchaseOrderAPI/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePurchaseOrder(int id, [FromBody] UpdatePurchaseOrderRequest request)
        {
            if (id != request.Id)
            {
                return BadRequest();
            }

            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.PurchaseOrderDetails)
                    .FirstOrDefaultAsync(po => po.Id == id);

                if (purchaseOrder == null)
                {
                    return NotFound();
                }

                // Cập nhật thông tin cơ bản
                purchaseOrder.SupplierName = request.SupplierName;
                purchaseOrder.Note = request.Note;

                // Xóa các PurchaseOrderDetails cũ
                _context.PurchaseOrderDetails.RemoveRange(purchaseOrder.PurchaseOrderDetails);

                // Thêm PurchaseOrderDetails mới
                foreach (var detailRequest in request.PurchaseOrderDetails)
                {
                    var purchaseOrderDetail = new PurchaseOrderDetail
                    {
                        PurchaseOrderId = purchaseOrder.Id,
                        ProductVariantId = detailRequest.ProductVariantId,
                        Quantity = detailRequest.Quantity,
                        ImportPrice = detailRequest.ImportPrice,
                        TotalPrice = detailRequest.Quantity * detailRequest.ImportPrice
                    };

                    _context.PurchaseOrderDetails.Add(purchaseOrderDetail);
                }

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi cập nhật đơn nhập hàng: " + ex.Message });
            }
        }

        // DELETE: api/PurchaseOrderAPI/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePurchaseOrder(int id)
        {
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.PurchaseOrderDetails)
                    .FirstOrDefaultAsync(po => po.Id == id);

                if (purchaseOrder == null)
                {
                    return NotFound();
                }

                // Trừ lại stock quantity của các ProductVariant
                foreach (var detail in purchaseOrder.PurchaseOrderDetails)
                {
                    var productVariant = await _context.ProductVariants
                        .FirstOrDefaultAsync(pv => pv.Id == detail.ProductVariantId);
                    
                    if (productVariant != null)
                    {
                        productVariant.StockQuantity -= (int)detail.Quantity;
                        if (productVariant.StockQuantity < 0)
                            productVariant.StockQuantity = 0;
                        _context.ProductVariants.Update(productVariant);
                    }
                }

                _context.PurchaseOrders.Remove(purchaseOrder);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Lỗi khi xóa đơn nhập hàng: " + ex.Message });
            }
        }

        // GET: api/PurchaseOrderAPI/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<object>> GetPurchaseOrderStatistics()
        {
            var totalOrders = await _context.PurchaseOrders.CountAsync();
            var totalValue = await _context.PurchaseOrderDetails
                .SumAsync(pod => pod.TotalPrice);
            var thisMonthOrders = await _context.PurchaseOrders
                .Where(po => po.CreatedAt >= DateTime.Now.AddDays(-30))
                .CountAsync();
            var thisMonthValue = await _context.PurchaseOrderDetails
                .Where(pod => pod.PurchaseOrder.CreatedAt >= DateTime.Now.AddDays(-30))
                .SumAsync(pod => pod.TotalPrice);

            return Ok(new
            {
                totalOrders,
                totalValue,
                thisMonthOrders,
                thisMonthValue
            });
        }

		private int GetCurrentUserId()
		{
			// Ưu tiên lấy token từ Header Authorization: Bearer <token>
			var authHeader = Request.Headers["Authorization"].FirstOrDefault();
			string? token = null;
			if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
			{
				token = authHeader.Substring("Bearer ".Length).Trim();
			}
			else
			{
				// Fallback: lấy từ cookie accessToken
				token = Request.Cookies["accessToken"];
			}

			if (string.IsNullOrWhiteSpace(token))
			{
				throw new UnauthorizedAccessException("Token không tồn tại.");
			}

			var handler = new JwtSecurityTokenHandler();
			JwtSecurityToken? jwtToken;
			try
			{
				jwtToken = handler.ReadToken(token) as JwtSecurityToken;
			}
			catch
			{
				throw new UnauthorizedAccessException("Token không hợp lệ.");
			}

			if (jwtToken == null)
			{
				throw new UnauthorizedAccessException("Không đọc được token.");
			}

			// Theo JwtService, claim lưu số điện thoại nằm ở JwtRegisteredClaimNames.Name
			var phoneNumber = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name)?.Value
				?? jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

			if (string.IsNullOrWhiteSpace(phoneNumber))
			{
				throw new UnauthorizedAccessException("Thiếu thông tin người dùng trong token.");
			}

			var user = _context.Users.FirstOrDefault(u => u.PhoneNumber == phoneNumber);
			if (user == null)
			{
				throw new UnauthorizedAccessException("Người dùng không tồn tại.");
			}

			return user.Id;
		}
    }

    // DTOs
    public class CreatePurchaseOrderRequest
    {
        public string SupplierName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<CreatePurchaseOrderDetailRequest> PurchaseOrderDetails { get; set; } = new();
    }

    public class CreatePurchaseOrderDetailRequest
    {
        public int ProductVariantId { get; set; }
        public int Quantity { get; set; }
        public decimal ImportPrice { get; set; }
    }

    public class UpdatePurchaseOrderRequest
    {
        public int Id { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<UpdatePurchaseOrderDetailRequest> PurchaseOrderDetails { get; set; } = new();
    }

    public class UpdatePurchaseOrderDetailRequest
    {
        public int? Id { get; set; }
        public int ProductVariantId { get; set; }
        public int Quantity { get; set; }
        public decimal ImportPrice { get; set; }
    }
}
