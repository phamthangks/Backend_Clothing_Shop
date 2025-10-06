namespace test1.Models.CheckoutModel
{
    public class Checkout
    {
        public int Id { get; set; }
        public int? ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string? Status { get; set; }

        public double? TotalMoney { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string Address { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string? Fullname { get; set; }
        public double Price { get; set; }
        public int? NumberOfProducts { get; set; }

		public List<int>? SelectedItems { get; set; }
	}
}
