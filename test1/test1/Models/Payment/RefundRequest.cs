namespace test1.Models.Payment
{
	public class RefundPayPalRequest
	{
		public string? CaptureId { get; set; }
		public decimal Amount { get; set; }
		public string? Currency { get; set; }
	}
}
