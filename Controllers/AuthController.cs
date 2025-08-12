using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        if (await _db.Users.AnyAsync(u => u.Email == registerDto.Email))
            return BadRequest(new { message = "Email already in use" });

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


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);
        if (user == null)
            return BadRequest(new { message = "Invalid credentials" });

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginDto.Password);
        if (result == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Invalid credentials" });

        // Change these to use Environment.GetEnvironmentVariable
        var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "YesList";
        var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "YesUsers";
        var jwtDuration = int.TryParse(Environment.GetEnvironmentVariable("JWT_DURATION"), out var duration) ? duration : 60;

        if (string.IsNullOrEmpty(jwtKey))
            return BadRequest(new { message = "JWT key not configured." });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email)
    };

        var token = new JwtSecurityToken(
     issuer: jwtIssuer,
     audience: jwtAudience,
     expires: DateTime.UtcNow.AddMinutes(jwtDuration),
     claims: claims,
     signingCredentials: new SigningCredentials(
         new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
         SecurityAlgorithms.HmacSha256)
 );


        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new
        {
            message = "Login successful",
            token = tokenString,
            expiration = token.ValidTo
        });
    }
}
