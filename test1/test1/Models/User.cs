using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class User
{
    public int Id { get; set; }

    public string? Fullname { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public string Password { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsActive { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public int? FacebookAccountId { get; set; }

    public int? GoogleAccountId { get; set; }

    public int? RoleId { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual Role? Role { get; set; }

    public virtual ICollection<ShippingAddress> ShippingAddresses { get; set; } = new List<ShippingAddress>();

    public virtual ICollection<SocialAccount> SocialAccounts { get; set; } = new List<SocialAccount>();

    public virtual ICollection<Token> Tokens { get; set; } = new List<Token>();
}
