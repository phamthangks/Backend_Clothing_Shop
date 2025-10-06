using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class Token
{
    public int Id { get; set; }

    public string TokenType { get; set; } = null!;

    public string Token1 { get; set; } = null!;

    public int? UserId { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public bool Revoked { get; set; }

    public bool Expired { get; set; }

    public virtual User? User { get; set; }
}
