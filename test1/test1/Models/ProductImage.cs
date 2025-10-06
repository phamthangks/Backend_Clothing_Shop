using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace test1.Models;

public partial class ProductImage
{
	public int Id { get; set; }

	public int? ProductId { get; set; }

	public string? ImageUrl { get; set; }
	[JsonIgnore]
	public virtual Product? Product { get; set; }
}
