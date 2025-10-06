using System.ComponentModel.DataAnnotations;

namespace test1.Models.AccountModel
{
    public class EditAddress
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? Fullname { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Address { get; set; }
    }
}
