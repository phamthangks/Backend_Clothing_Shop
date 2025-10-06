namespace test1.Models.Payment
{
	public class PaymentRequest
	{
		public int OrderCode { get; set; }
		public int Amount { get; set; }
		public string? Description { get; set; }
		public string? BuyerName { get; set; }
		public string? BuyerEmail { get; set; }
		public string? BuyerPhone { get; set; }
		public string? BuyerAddress { get; set; }
		public List<Item>? Items { get; set; }
		public string? CancelUrl { get; set; }
		public string? ReturnUrl { get; set; }
		public int ExpiredAt { get; set; }
		public string? Signature { get; set; }
	}

	public class Item
	{
		public string? Name { get; set; }
		public int Quantity { get; set; }
		public int Price { get; set; }
	}

	public class PaymentResponse
	{
		public string? Code { get; set; }
		public string? Desc { get; set; }
		public PaymentData? Data { get; set; }
		public string? Signature { get; set; }
	}

	public class PaymentData
	{
		public string? PaymentLinkId { get; set; }
		public int OrderCode { get; set; }
		public int Amount { get; set; }
		public string? Status { get; set; }
		public string? CheckoutUrl { get; set; }
		// Các trường khác theo response...
	}
	public class CancelRequest
	{
		public string? CancellationReason { get; set; }
	}


}
