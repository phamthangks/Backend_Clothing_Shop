using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

public class PayPalService
{
	private readonly IConfiguration _configuration;
	private readonly HttpClient _httpClient;

	public PayPalService(IConfiguration configuration, HttpClient httpClient)
	{
		_configuration = configuration;
		_httpClient = httpClient;
	}

	private async Task<string> GetAccessTokenAsync()
	{
		var clientId = _configuration["PayPal:ClientId"];
		var secret = _configuration["PayPal:ClientSecret"];
		var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));

		var request = new HttpRequestMessage(HttpMethod.Post, "https://api-m.sandbox.paypal.com/v1/oauth2/token");
		request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);
		request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			{ "grant_type", "client_credentials" }
		});

		var response = await _httpClient.SendAsync(request);
		response.EnsureSuccessStatusCode();

		var content = await response.Content.ReadAsStringAsync();
		dynamic tokenResponse = JsonConvert.DeserializeObject(content);
		return tokenResponse.access_token;
	}
	public async Task<bool> RefundPaymentAsync(string captureId, decimal refundAmount, string currencyCode = "USD")
	{
		var accessToken = await GetAccessTokenAsync();
		var refundUrl = $"https://api-m.sandbox.paypal.com/v2/payments/captures/{captureId}/refund";

		var refundData = new
		{
			amount = new
			{
				value = refundAmount.ToString("F2"),
				currency_code = currencyCode
			}
		};

		var json = JsonConvert.SerializeObject(refundData);
		var content = new StringContent(json, Encoding.UTF8, "application/json");

		var request = new HttpRequestMessage(HttpMethod.Post, refundUrl);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		request.Content = content;

		var response = await _httpClient.SendAsync(request);
		return response.IsSuccessStatusCode;
	}
}
