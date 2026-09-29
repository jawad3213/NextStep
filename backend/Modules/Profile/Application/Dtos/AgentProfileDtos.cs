namespace NextStep.Modules.Profile.Application.Dtos;

/// <summary>GET /internal/agents/users/{userRef}: who a Keycloak or local id designates.</summary>
public sealed record AgentUserDto(Guid UserId, string? FirstName, string? LastName, string? Email);

/// <summary>GET /internal/agents/profiles/{userRef}: the full profile, for the Python agents.</summary>
public sealed record AgentProfileDto(Guid UserId, FullProfileDto Profile);
