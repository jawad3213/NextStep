namespace NextStep.Modules.Sourcing.Application.Dtos;

/// <summary>502 body when the agents service fails: the standard <c>error</c> plus upstream details.</summary>
public sealed record AgentsProxyErrorResponse(string Error, string Detail, string Path);
