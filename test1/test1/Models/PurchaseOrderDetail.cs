using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class PurchaseOrderDetail
{
    public int Id { get; set; }

    public int? PurchaseOrderId { get; set; }

    public int? ProductVariantId { get; set; }

    public int? Quantity { get; set; }

    public decimal? ImportPrice { get; set; }

    public decimal? TotalPrice { get; set; }

    public virtual ProductVariant? ProductVariant { get; set; }

    public virtual PurchaseOrder? PurchaseOrder { get; set; }
}
