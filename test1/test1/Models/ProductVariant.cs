using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class ProductVariant
{
    public int Id { get; set; }

    public string Color { get; set; } = null!;

    public string Size { get; set; } = null!;

    public double Price { get; set; }

    public int StockQuantity { get; set; }

    public int ProductId { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual Product Product { get; set; } = null!;
}
