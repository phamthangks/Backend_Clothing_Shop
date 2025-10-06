using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class SocialAccount
{
    public int Id { get; set; }

    public string Provider { get; set; } = null!;

    public string ProviderId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int? UserId { get; set; }

    public virtual User? User { get; set; }
}
