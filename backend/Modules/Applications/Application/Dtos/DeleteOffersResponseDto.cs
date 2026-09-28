namespace NextStep.Modules.Applications.Application.Dtos;

/// <summary>Result of a bulk offer deletion: <c>{ "deletedCount": n }</c>.</summary>
public sealed record DeleteOffersResponseDto(int DeletedCount);
