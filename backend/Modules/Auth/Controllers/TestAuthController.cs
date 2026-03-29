using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Modules.Auth.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestAuthController : ControllerBase
    {
        [HttpGet("public")]
        public IActionResult PublicEndpoint()
        {
            return Ok(new { message = "Ceci est un endpoint public." });
        }

        [HttpGet("protected")]
        [Authorize]
        public IActionResult ProtectedEndpoint()
        {
            var user = User.Identity?.Name ?? "Utilisateur Anonyme";
            return Ok(new 
            { 
                message = "Ceci est un endpoint privé.", 
                user = user,
                claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
    }
}
