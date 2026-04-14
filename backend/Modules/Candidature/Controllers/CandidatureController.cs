using System.Security.Claims;
using backend.Modules.Candidature.DTOs;
using backend.Modules.Candidature.Services;
using backend.Modules.Identity.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Modules.Candidature.Controllers;

[ApiController]
[Route("api/candidatures")]
[Authorize]
public class CandidatureController : ControllerBase
{
    private readonly ICandidatureService _candidatureService;
    private readonly IUserRepository _userRepository;

    public CandidatureController(
        ICandidatureService candidatureService,
        IUserRepository userRepository)
    {
        _candidatureService = candidatureService;
        _userRepository = userRepository;
    }

    [HttpPost]
    public async Task<ActionResult<CandidatureDto>> Create(
        [FromBody] CreateCandidatureDto dto,
        CancellationToken cancellationToken)
    {
        var keycloakId = User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(keycloakId))
            return Unauthorized("Missing Keycloak identifier.");

        var user = await _userRepository.GetByKeycloakIdAsync(keycloakId);

        if (user is null)
            return Unauthorized("User not found in local database.");

        var result = await _candidatureService.CreateAsync(user.Id, dto, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.IdCandidature }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidatureDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _candidatureService.GetByIdAsync(id, cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }
}