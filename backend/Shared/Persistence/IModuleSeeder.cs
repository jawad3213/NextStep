namespace NextStep.Shared.Persistence;

/// <summary>
/// Reference data a module needs at startup (e.g. skill keywords, CV templates).
/// Runs after migrations, on every start: implementations must be idempotent.
/// </summary>
public interface IModuleSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}
