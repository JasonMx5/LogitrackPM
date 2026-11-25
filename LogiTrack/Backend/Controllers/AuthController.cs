using System;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using LogiTrack.Email;
using LogiTrack.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LogiTrack.Controllers
{
    [Route("api/auth")]
    [ApiController]
    [ServiceFilter(typeof(LogiTrack.Security.AuthRateLimitFilter))]
    public class AuthController : ControllerBase
    {
        private const int AccessTokenLifetimeMinutes = 15;
        private const string GenericAccountMessage =
            "If the account can be verified or recovered, instructions will be emailed.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            IEmailSender emailSender,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _emailSender = emailSender;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthRequest request)
        {
            var email = request.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                if (result.Errors.Any(error =>
                    error.Code == "DuplicateUserName" || error.Code == "DuplicateEmail"))
                {
                    return Accepted(new { message = GenericAccountMessage });
                }

                return BadRequest(new
                {
                    errors = result.Errors.Select(error => error.Description)
                });
            }

            await TrySendConfirmationEmailAsync(user);
            return Accepted(new { message = GenericAccountMessage });
        }

        [HttpPost("resend-confirmation")]
        public async Task<IActionResult> ResendConfirmation([FromBody] EmailRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user != null && !user.EmailConfirmed)
            {
                await TrySendConfirmationEmailAsync(user);
            }

            return Accepted(new { message = GenericAccountMessage });
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(
            [FromQuery] string userId,
            [FromQuery] string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(new { message = "The confirmation link is invalid or expired." });
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "The confirmation link is invalid or expired." });
            }

            return Ok(new { message = "Email address confirmed." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user == null)
            {
                return StatusCode(
                    StatusCodes.Status401Unauthorized,
                    new { message = "Invalid email or password." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return StatusCode(
                    StatusCodes.Status401Unauthorized,
                    new { message = "Invalid email or password." });
            }

            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(AccessTokenLifetimeMinutes);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }.ToList();
            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                notBefore: now,
                expires: expiresAt,
                signingCredentials: credentials);

            return Ok(new
            {
                accessToken = new JwtSecurityTokenHandler().WriteToken(token),
                tokenType = "Bearer",
                expiresAt
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] EmailRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user != null && user.EmailConfirmed)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetEndpoint = GetPublicApiBaseUrl() + "/api/auth/reset-password";
                var message =
                    "Submit a POST request to " + resetEndpoint + " with this JSON body:\n\n" +
                    "{\n" +
                    "  \"userId\": \"" + user.Id + "\",\n" +
                    "  \"token\": \"" + token + "\",\n" +
                    "  \"newPassword\": \"your new password\"\n" +
                    "}";

                await TrySendEmailAsync(user.Email, "Reset your LogiTrack password", message);
            }

            return Accepted(new { message = GenericAccountMessage });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return BadRequest(new { message = "The password reset request is invalid or expired." });
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors = result.Errors.Select(error => error.Description)
                });
            }

            return NoContent();
        }

        private string GetPublicApiBaseUrl()
        {
            var url = _configuration["Auth:PublicApiBaseUrl"];
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps &&
                 !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            {
                throw new InvalidOperationException(
                    "Configure Auth:PublicApiBaseUrl with an absolute HTTPS URL.");
            }

            return url.TrimEnd('/');
        }

        private async Task TrySendConfirmationEmailAsync(ApplicationUser user)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationUrl = GetPublicApiBaseUrl() + "/api/auth/confirm-email?userId=" +
                Uri.EscapeDataString(user.Id) + "&token=" + Uri.EscapeDataString(token);

            await TrySendEmailAsync(
                user.Email,
                "Confirm your LogiTrack email",
                "Confirm your email address by opening this link:\n" + confirmationUrl);
        }

        private async Task TrySendEmailAsync(string recipient, string subject, string body)
        {
            try
            {
                await _emailSender.SendAsync(recipient, subject, body);
            }
            catch (EmailDeliveryException exception)
            {
                _logger.LogError(exception, "Unable to deliver authentication email.");
            }
        }
    }

    public class AuthRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; }

        [Required]
        [StringLength(256, MinimumLength = 1)]
        public string Password { get; set; }
    }

    public class EmailRequest
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        [StringLength(256, MinimumLength = 12)]
        public string NewPassword { get; set; }
    }
}