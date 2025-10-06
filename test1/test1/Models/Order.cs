using System;
using System.Collections.Generic;

namespace test1.Models;

public partial class Order
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public string? Fullname { get; set; }

    public string? Email { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime? OrderDate { get; set; }

    public string? Status { get; set; }

    public double? TotalMoney { get; set; }

    public string? ShippingMethod { get; set; }

    public DateOnly? ShippingDate { get; set; }

    public string? TrackingNumber { get; set; }

    public string? PaymentMethod { get; set; }

    public bool? Active { get; set; }

    public bool IsQuickPurchase { get; set; }

    public string? CaptureId { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual User? User { get; set; }
}
