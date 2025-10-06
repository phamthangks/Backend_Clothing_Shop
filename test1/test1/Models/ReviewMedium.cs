using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class ReviewMedium
{
    public int MediaId { get; set; }

    public int ReviewId { get; set; }

    public string MediaType { get; set; } = null!;

    public string MediaUrl { get; set; } = null!;

    public virtual Review Review { get; set; } = null!;
}
