namespace test1.Models.AccountModel
{
    public class Account
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
		public int? OrderId { get; set; }
		public string Name { get; set; } = null!;
        public string? Status { get; set; }
		

		public string? ProductImageUrl { get; set; }
		public string? PaymentMethod { get; set; }

		public string? CaptureId { get; set; }
		public double? TotalMoney { get; set; }
        public DateTime? OrderDate { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string? Fullname { get; set; }
		public string PhoneNumberS { get; set; } = null!;
		public string? FullnameS { get; set; }
		public string? AddressS { get; set; }
		public double Price { get; set; }
        public int? NumberOfProducts { get; set; }
        public bool? IsActive { get; set; }
    }
}
