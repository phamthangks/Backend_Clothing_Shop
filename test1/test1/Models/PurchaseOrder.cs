using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class PurchaseOrder
{
    public int Id { get; set; }

    public string? SupplierName { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? Note { get; set; }

    public int? UserId { get; set; }

    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();

    public virtual User? User { get; set; }
}
