using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using AuthTodoApp.Data;
using AuthTodoApp.Models;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IPasswordHasher<IdentityUser> _passwordHasher;

    public AuthController(ApplicationDbContext db, IConfiguration config, IPasswordHasher<IdentityUser> passwordHasher)
    {
        _db = db;
        _config = config;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserRegisterDto registerDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Fix: Use registerDto.Email instead of model.Email
        if (!new EmailAddressAttribute().IsValid(registerDto.Email))
        {
            return BadRequest(new { message = "Invalid email format" });
        }

        if (await _db.Users.AnyAsync(u => u.Email == registerDto.Email))
            return BadRequest(new { message = "Email already in use" });

        try
        {
            var user = new IdentityUser
            {
                UserName = registerDto.Email,
                Email = registerDto.Email
            };

            // Hash password
            user.PasswordHash = _passwordHasher.HashPassword(user, registerDto.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new { message = "User registered successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Registration failed. Please try again." });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid credentials" });

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginDto.Password);
            if (result == PasswordVerificationResult.Failed)
                return BadRequest(new { message = "Invalid credentials" });

            // Get JWT configuration from environment variables or config
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? _config["JWT:Key"];
            var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? _config["JWT:Issuer"] ?? "YesList";
            var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? _config["JWT:Audience"] ?? jwtIssuer;
            var jwtDuration = int.TryParse(Environment.GetEnvironmentVariable("JWT_DURATION") ?? _config["JWT:Duration"], out var duration) ? duration : 60;

            if (string.IsNullOrEmpty(jwtKey))
                return StatusCode(500, new { message = "JWT configuration error" });

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                expires: DateTime.UtcNow.AddMinutes(jwtDuration),
                claims: claims,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new
            {
                message = "Login successful",
                token = tokenString,
                expiration = token.ValidTo
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Login failed. Please try again." });
        }
    }
}