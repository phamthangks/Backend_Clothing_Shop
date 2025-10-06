namespace test1.Models.Authentication
{
    public class LoginResponseModel
    {
        public string? PhoneNumber { get; set; }
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}
