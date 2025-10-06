namespace test1.Models.AccountModel
{
	public class ReviewCreateModel
	{
		public int ProductId { get; set; }
		public int Rating { get; set; }
		public string? ReviewText { get; set; }
		public List<IFormFile>? MediaFiles { get; set; }
	}
}
