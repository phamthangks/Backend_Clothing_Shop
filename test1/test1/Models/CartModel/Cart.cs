namespace test1.Models.CartModel
{
    public class Cart
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
        public int? ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string? Image { get; set; }
        public double Price { get; set; }
        public int? NumberOfProducts { get; set; }
        public bool IsSelectedForPurchase { get; set; }
    }
}
