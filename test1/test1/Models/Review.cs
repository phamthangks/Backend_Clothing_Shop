using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class Review
{
    public int ReviewId { get; set; }

    public int UserId { get; set; }

    public int ProductId { get; set; }

    public byte Rating { get; set; }

    public string? ReviewText { get; set; }

    public DateTime? ReviewDate { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ReviewMedium> ReviewMedia { get; set; } = new List<ReviewMedium>();

    public virtual User User { get; set; } = null!;
}
