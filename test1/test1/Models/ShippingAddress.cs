using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace test1.Models;

public partial class ShippingAddress
{
	public int Id { get; set; }

	public int UserId { get; set; }

	public string Address { get; set; } = null!;

	public string PhoneNumber { get; set; } = null!;

	public bool? IsDefault { get; set; }

	public string? Fullname { get; set; }
	[JsonIgnore]
	public virtual User User { get; set; } = null!;
}
