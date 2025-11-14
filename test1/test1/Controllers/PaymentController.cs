using Microsoft.AspNetCore.Mvc;
using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;
using PayPalCheckoutSdk.Payments;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using test1.Models;
using test1.Models.Payment;
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
	private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _secretKey = "afb8bb3c8086b43aac0e8d685dea05891f7ced7e9c4bb4cd0a94e848e6587ef6";
    private readonly string _clientId = "0c9ef1c9-b0ea-4ff7-a880-345c0fa8f789";
    private readonly string _apiKey = "951e09b1-0b21-466a-862c-a8e97df8045f";
    // Nếu có partner code, hãy cấu hình tại đây, nếu không, bạn có thể bỏ qua header này
    private readonly string _partnerCode = "";
	private object responseContent;
	private ILogger _logger;
	private readonly PayPalEnvironment _environment;
	private readonly PayPalHttpClient _client;
	private readonly PayPalService _payPalService;
	private readonly QlbanQuanAoContext _context;
	public PaymentController(IHttpClientFactory httpClientFactory, IConfiguration configuration, PayPalService payPalService, QlbanQuanAoContext context)
	{
		_httpClientFactory = httpClientFactory;
		// Lấy ClientId và ClientSecret từ appsettings.json
		var clientId = configuration["PayPal:ClientId"];
		var clientSecret = configuration["PayPal:ClientSecret"];
		_context = context;
		// Sử dụng SandboxEnvironment; nếu bạn triển khai thực, hãy chuyển sang LiveEnvironment
		_environment = new SandboxEnvironment(clientId, clientSecret);
		_client = new PayPalHttpClient(_environment);
		_payPalService = payPalService;
	}
	/// <summary>
	/// Endpoint callback để capture thanh toán từ PayPal và lấy CaptureId.
	/// Đây là URL mà PayPal chuyển hướng sau khi khách hàng phê duyệt thanh toán.
	/// Ví dụ: https://your-domain/api/payment/capture-payment?token={paypalOrderId}&orderId={yourOrderId}
	/// </summary>
	[HttpGet("capture-payment")]
	public async Task<IActionResult> CapturePayment([FromQuery] string token, [FromQuery] int orderId)
	{
		try
		{
			// Tạo yêu cầu capture với OrderId (token) nhận từ PayPal
			var captureRequest = new OrdersCaptureRequest(token);
			captureRequest.RequestBody(new OrderActionRequest());

			// Gọi API Capture
			var response = await _client.Execute(captureRequest);
			var captureResult = response.Result<PayPalCheckoutSdk.Orders.Order>();

			// Lấy CaptureId từ purchase units đầu tiên (nếu có nhiều thì cần xử lý phù hợp)
			var captureId = captureResult.PurchaseUnits
				.FirstOrDefault()?
				.Payments?
				.Captures?
				.FirstOrDefault()?
				.Id;

			if (string.IsNullOrEmpty(captureId))
			{
				return BadRequest(new { message = "CaptureId không được tìm thấy." });
			}

			// Tìm đơn hàng tương ứng trong cơ sở dữ liệu
			var order = _context.Orders.FirstOrDefault(o => o.Id == orderId);
			if (order == null)
			{
				return NotFound(new { message = "Đơn hàng không tồn tại." });
			}

			// Cập nhật CaptureId cho đơn hàng
			order.CaptureId = captureId;
			await _context.SaveChangesAsync();

			return Ok(new { message = "Thanh toán thành công và CaptureId đã được lưu.", captureId });
		}
		catch (Exception ex)
		{
			return StatusCode(500, new { message = "Lỗi khi capture thanh toán", error = ex.Message });
		}
	}

	//PayPal
	[HttpPost("create-payment")]
	public async Task<IActionResult> CreatePayment([FromBody] PaymentPaypalRequest request)
	{
		var orderRequest = new OrdersCreateRequest();
		orderRequest.Prefer("return=representation");
		orderRequest.RequestBody(new OrderRequest()
		{
			CheckoutPaymentIntent = "CAPTURE",
			PurchaseUnits = new List<PurchaseUnitRequest>()
				{
					new PurchaseUnitRequest
					{
						ReferenceId = request.orderCode.ToString(),
						AmountWithBreakdown = new AmountWithBreakdown
						{
							CurrencyCode = "USD",
							Value = request.amount.ToString()
						},
						Description = request.description
					}
				},
			ApplicationContext = new ApplicationContext
			{
				ReturnUrl = request.returnUrl,
				CancelUrl = request.cancelUrl
			}
		});

		try
		{
			var response = await _client.Execute(orderRequest);
			var result = response.Result<PayPalCheckoutSdk.Orders.Order>();

			// Lấy link duyệt thanh toán (approval link)
			var approvalLink = result.Links.FirstOrDefault(link =>
				string.Equals(link.Rel, "approve", StringComparison.OrdinalIgnoreCase))?.Href;

			return Ok(new PaymentPaypalResponse
			{
				code = "00",
				desc = "Success",
				data = new PaymentPaypalData { checkoutUrl = approvalLink }
			});
		}
		catch (Exception ex)
		{
			return BadRequest(new PaymentPaypalResponse
			{
				code = "01",
				desc = ex.Message,
				data = null
			});
		}
	}
	[HttpPost("refund")]
	public async Task<IActionResult> Refund([FromBody] RefundPayPalRequest request)
	{
		// Kiểm tra logic hủy đơn và lấy captureId từ đơn hàng đã thanh toán
		var result = await _payPalService.RefundPaymentAsync(request.CaptureId, request.Amount, request.Currency);
		if (result)
			return Ok(new { message = "Refund successful" });
		return BadRequest(new { message = "Refund failed" });
	}
	//PayOS
    [HttpPost("create")]
    public async Task<IActionResult> CreatePaymentLink([FromBody] PaymentRequest request)
    {
        // Tạo chữ ký
        request.Signature = GenerateSignature(request, _secretKey);

        // **Đóng gói payload theo form mà PayOS yêu cầu**
        // NHIỀU API pay gateway yêu cầu: { "request": { ... } } -> kiểm tra doc PayOS, nếu đúng thì dùng structure này
        var payload = new
        {
            request = new
            {
                orderCode = request.OrderCode,
                amount = request.Amount, // đảm bảo đây là số nguyên (int)
                description = request.Description,
                buyerName = request.BuyerName,
                buyerEmail = request.BuyerEmail,
                buyerPhone = request.BuyerPhone,
                buyerAddress = request.BuyerAddress,
                items = request.Items,
                cancelUrl = request.CancelUrl,
                returnUrl = request.ReturnUrl,
                expiredAt = request.ExpiredAt,
                signature = request.Signature
            }
        };

        var payosUrl = "https://api-merchant.payos.vn/v2/payment-requests";

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Remove("x-client-id");
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Remove("x-partner-code");

        client.DefaultRequestHeaders.Add("x-client-id", _clientId);
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        if (!string.IsNullOrEmpty(_partnerCode))
            client.DefaultRequestHeaders.Add("x-partner-code", _partnerCode);

        // Gửi và đọc content trả về
        var response = await client.PostAsJsonAsync(payosUrl, payload);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            // Parse về model nếu cần
            var result = await response.Content.ReadFromJsonAsync<PaymentResponse>();
            return Ok(result);
        }
        else
        {
            // Log rõ nội dung trả về từ PayOS
            // _logger phải được inject (ILogger<PaymentController>)
            _logger?.LogError("PayOS create payment error: {Status} - {Body}", response.StatusCode, responseBody);

            // Trả nguyên body lỗi từ PayOS về client để FE dễ debug
            // Có thể muốn parse trước khi trả, ở đây trả raw string
            return BadRequest(new { message = "PayOS error", detail = responseBody });
        }
    }

    [HttpPost("cancel/{id}")]
	public async Task<IActionResult> CancelPayment(int id, [FromBody] CancelRequest request)
	{
		// Tạo payload cho API payOS
		var payload = new { cancellationReason = request.CancellationReason };

		// URL hủy đơn hàng, với {id} là mã đơn hàng hoặc paymentLinkId
		var cancelUrl = $"https://api-merchant.payos.vn/v2/payment-requests/{id}/cancel";

		var client = _httpClientFactory.CreateClient();
		// Thêm header xác thực
		client.DefaultRequestHeaders.Add("x-client-id", _clientId);
		client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
		if (!string.IsNullOrEmpty(_partnerCode))
		{
			client.DefaultRequestHeaders.Add("x-partner-code", _partnerCode);
		}

		var response = await client.PostAsJsonAsync(cancelUrl, payload);
		if (response.IsSuccessStatusCode)
		{
			// Nếu thành công, trả về dữ liệu từ payOS
			var result = await response.Content.ReadFromJsonAsync<object>(); // Có thể tạo model PaymentResponse tương tự như trước
			return Ok(result);
		}
		else
		{
			var errorContent = await response.Content.ReadAsStringAsync();
			return BadRequest(errorContent);
		}
	}
	private string GenerateSignature(PaymentRequest request, string secretKey)
	{
		// Tạo chuỗi dữ liệu theo thứ tự alphabet:
		// "amount=$amount&cancelUrl=$cancelUrl&description=$description&orderCode=$orderCode&returnUrl=$returnUrl"
		var data = $"amount={request.Amount}&cancelUrl={request.CancelUrl}&description={request.Description}&orderCode={request.OrderCode}&returnUrl={request.ReturnUrl}";

		using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
		{
			var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
			return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
		}
	}
}
