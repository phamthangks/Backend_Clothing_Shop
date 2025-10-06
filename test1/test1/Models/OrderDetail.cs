using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class OrderDetail
{
    public int Id { get; set; }

    public int? OrderId { get; set; }

    public int? ProductId { get; set; }

    public double? Price { get; set; }

    public int? NumberOfProducts { get; set; }

    public double? TotalMoney { get; set; }

    public string Status { get; set; } = null!;

    public virtual Order? Order { get; set; }

    public virtual Product? Product { get; set; }
}
