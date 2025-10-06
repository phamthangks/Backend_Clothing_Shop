namespace test1.Models.CheckoutModel
{
	public class CheckoutRequest
	{
		public int OrderId { get; set; }
		public List<int> SelectedItems { get; set; }
	}
}
