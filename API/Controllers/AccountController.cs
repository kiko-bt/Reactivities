using API.DTOs;
using API.Services;
using Domain;
using Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace API.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly TokenService _tokenService;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly SendEmail _sendEmail;

        public AccountController(UserManager<AppUser> userManager, 
               SignInManager<AppUser> signInManager, 
               TokenService tokenService, 
               IConfiguration config, 
               SendEmail sendEmail)
        {
            _signInManager = signInManager;
            _tokenService = tokenService;
            _config = config;
            _userManager = userManager;
            _sendEmail = sendEmail;
            _httpClient = new HttpClient()
            {
                BaseAddress = new System.Uri("https://graph.facebook.com")
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDTO>> Login(LoginDTO loginDTO)
        {
            var user = await _userManager.Users.Include(p => p.Photos)
                                               .FirstOrDefaultAsync(x => x.Email == loginDTO.Email);

            if (user == null) return Unauthorized("Invalid email");

            if (user.UserName == "bob" || user.UserName == "tom" || user.UserName == "jane") user.EmailConfirmed = true;

            if (!user.EmailConfirmed) return Unauthorized("Email not confirmed");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDTO.Password, false);

            if (result.Succeeded)
            {
                await SetRefreshToken(user);
                return CreateUserObject(user);
            }

            return Unauthorized("Invalid password");
        }


        [HttpPost("register")]
        public async Task<ActionResult<UserDTO>> Register(RegisterDTO registerDTO)
        {
            if (await _userManager.Users.AnyAsync(x => x.Email == registerDTO.Email))
            {
                ModelState.AddModelError("email", "Email taken");
                return ValidationProblem();
            }
            if (await _userManager.Users.AnyAsync(x => x.UserName == registerDTO.Username))
            {
                ModelState.AddModelError("username", "Username taken");
                return ValidationProblem();
            }

            var user = new AppUser
            {
                DisplayName = registerDTO.DisplayName,
                Email = registerDTO.Email,
                UserName = registerDTO.Username
            };

            var result = await _userManager.CreateAsync(user, registerDTO.Password);

            if (!result.Succeeded) return BadRequest("Problem registering user");

            var origin = Request.Headers["origin"];

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var verifyUrl = $"{origin}/account/verifyEmail?token={token}&email={user.Email}";
            var message = $"<p>Please click the below link to verify your email addresss:</p><p><a href='{verifyUrl}'>Click to verify email</a></p>";

            await _sendEmail.SendEmailAsync(user.Email, "Please verify email", message);

            return Ok("Registration success - please verify your email");
        }

        [AllowAnonymous]
        [HttpPost("verifyEmail")]
        public async Task<IActionResult> VerifyEmail(string token, string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return Unauthorized();
            var decodedTokenBytes = WebEncoders.Base64UrlDecode(token);
            var decodenToken = Encoding.UTF8.GetString(decodedTokenBytes);
            var result = await _userManager.ConfirmEmailAsync(user, decodenToken);

            if (!result.Succeeded) return BadRequest("Could not verify email address");

            return Ok("Email confirmed - you can now login");
        }

        [AllowAnonymous]
        [HttpGet("resendEmailConfirmationLink")]
        public async Task<IActionResult> ResendEmailConfirmationLink(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            if (user is null) return Unauthorized();

            var origin = Request.Headers["origin"];

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            var verifyUrl = $"{origin}/account/verifyEmail?token={token}&email={user.Email}";
            var message = $"<p>Please click the below link to verify your email addresss:</p><p><a href='{verifyUrl}'>Click to verify email</a></p>";

            await _sendEmail.SendEmailAsync(user.Email, "Please verify email", message);

            return Ok("Email verification link resent");
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<UserDTO>> GetCurrentUser()
        {
            var user = await _userManager.Users.Include(p => p.Photos)
                                               .FirstOrDefaultAsync(x => x.Email == User.FindFirstValue(ClaimTypes.Email));


            await SetRefreshToken(user);
            return CreateUserObject(user);
        }

        [AllowAnonymous]
        [HttpPost("fbLogin")]
        public async Task<ActionResult<UserDTO>> FacebookLogin(string accessToken)
        {
            var fbVarifyKeys = _config["Facebook:AppId"] + "|" + _config["Facebook:ApiSecret"];

            var verifyTokenResponse = await _httpClient
                .GetAsync($"debug_token?input_token={accessToken}&access_token={fbVarifyKeys}");

            if (!verifyTokenResponse.IsSuccessStatusCode) return Unauthorized();

            var fbUrl = $"me?access_token={accessToken}&fields=name,email,picture.width(100).height(100)";

            var fbInfo = await _httpClient.GetFromJsonAsync<FacebookDTO>(fbUrl);

            var user = _userManager.Users.Include(p => p.Photos).FirstOrDefault(x => x.Email == fbInfo.Email);

            if (user != null) return CreateUserObject(user);

            user = new AppUser
            {
                DisplayName = fbInfo.Name,
                Email = fbInfo.Email,
                UserName = fbInfo.Email,
                Photos = new List<Photo>
                {
                    new Photo
                    {
                        Id = "fb" + fbInfo.Id,
                        Url = fbInfo.Picture.FacebookPictureData.Url,
                        IsMain = true
                    }
                }
            };

            var result = await _userManager.CreateAsync(user);

            if (!result.Succeeded) return BadRequest("Problem creating user account");


            await SetRefreshToken(user);
            return CreateUserObject(user);
        }

        [Authorize]
        [HttpPost("refreshToken")]
        public async Task<ActionResult<UserDTO>> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            var user = await _userManager.Users
                        .Include(r => r.RefreshTokens)
                        .Include(p => p.Photos)
                        .FirstOrDefaultAsync(x => x.UserName == User.FindFirstValue(ClaimTypes.Name));

            if (user == null) return Unauthorized();

            var oldToken = user.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken);

            if (oldToken != null && !oldToken.IsActive) return Unauthorized();

            if (oldToken != null) oldToken.Revoked = DateTime.UtcNow;
            
            return CreateUserObject(user);
        }

        private async Task SetRefreshToken(AppUser user)
        {
            var refreshToken = _tokenService.GenerateRefreshToken();

            user.RefreshTokens.Add(refreshToken);
            await _userManager.UpdateAsync(user);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7)
            };

            Response.Cookies.Append("refreshToken", refreshToken.Token, cookieOptions);
        }


        private UserDTO CreateUserObject(AppUser user)
        {
            return new UserDTO
            {
                DisplayName = user.DisplayName,
                Image = user?.Photos?.FirstOrDefault(x => x.IsMain)?.Url,
                Token = _tokenService.CreateToken(user),
                Username = user.UserName
            };
        }
    }
}
