namespace test1.Models.Payment
{
	public class PaymentPaypalRequest
	{
		public int orderCode { get; set; }
		public decimal amount { get; set; }
		public string? description { get; set; }
		public string? buyerName { get; set; }
		public string? buyerEmail { get; set; }
		public string? buyerPhone { get; set; }
		public string? buyerAddress { get; set; }
		public List<PaymentPaypalItem>? items { get; set; }
		public string? cancelUrl { get; set; }
		public string? returnUrl { get; set; }
		public int expiredAt { get; set; }
	}

	public class PaymentPaypalItem
	{
		public string? name { get; set; }
		public int? quantity { get; set; }
		public decimal price { get; set; }
	}

	public class PaymentPaypalResponse
	{
		public string? code { get; set; }
		public string? desc { get; set; }
		public PaymentPaypalData? data { get; set; }
	}

	public class PaymentPaypalData
	{
		public string? checkoutUrl { get; set; }
	}
}
