using Microsoft.AspNetCore.Mvc;
using test1.Models;

namespace test1.Areas.Admin.Controllers
{
	[Area("admin")]
	[Route("api/roles")]
	[ApiController]
	public class RolesAPIController : ControllerBase
	{
		private readonly QlbanQuanAoContext _context;

		public RolesAPIController(QlbanQuanAoContext context)
		{
			_context = context;
		}

		[HttpGet]
		public IActionResult GetAll()
		{
			var roles = _context.Roles
				.Select(r => new { r.Id, r.Name })
				.ToList();
			return Ok(roles);
		}
	}
}



