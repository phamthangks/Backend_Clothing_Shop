using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using test1.Handlers;
using test1.Models;

namespace test1.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class UserAccountAPIController : ControllerBase
	{
		private readonly QlbanQuanAoContext _context;

		public UserAccountAPIController(QlbanQuanAoContext context)
		{
			_context = context;
		}
		[HttpGet]
		public async Task<List<User>> Get()
		{
			return await _context.Users.ToListAsync();
		}
		[HttpGet("{id}")]
		public async Task<User> GetById(int id)
		{
			return await _context.Users.FirstOrDefaultAsync(x => x.Id == id);
		}
		[HttpPost]
		public async Task<ActionResult> Create([FromBody] User userAccount)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}

			userAccount.Password = PasswordHashHandler.HashPassword(userAccount.Password);
			userAccount.RoleId = 2;
			await _context.Users.AddAsync(userAccount);
			await _context.SaveChangesAsync();

			return CreatedAtAction(nameof(GetById), new { id = userAccount.Id }, userAccount);
		}
		[HttpPut]
		public async Task<ActionResult> Update([FromBody] User userAccount)
		{
			if (userAccount.Id == 0 ||
				string.IsNullOrWhiteSpace(userAccount.Fullname) ||
				string.IsNullOrWhiteSpace(userAccount.PhoneNumber) ||
				string.IsNullOrWhiteSpace(userAccount.Password))
			{
				return BadRequest("Invalid Request");
			}
			userAccount.Password = PasswordHashHandler.HashPassword(userAccount.Password);
			_context.Users.Update(userAccount);
			await _context.SaveChangesAsync();
			return Ok();
		}
	}
}
