using api.Data;
using api.Models;
using api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;

namespace api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;
        public AuthController(AppDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public IActionResult Login(User login)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var user = _context.Users.Find(login.Email);

            if (user == null)
            {
                return NotFound();
            }

            if (user.Password != login.Password)
            {
                return Unauthorized();
            }

            var response = new
            {
                Email = user.Email,
                Token = _tokenService.GenerateToken(user)
            };

            return Ok(response);
        }

        [HttpPost("register")]
        public IActionResult Register(User register)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var user = _context.Users.Find(register.Email);

            if (user != null)
            {
                return Conflict();
            }

            _context.Users.Add(register);
            _context.SaveChanges();

            var response = new
            {
                Email = register.Email,
                Token = _tokenService.GenerateToken(register)
            };

            return CreatedAtAction(nameof(Login), response);
        }
    }
}
