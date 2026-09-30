namespace NextStep.Shared.Persistence;

/// <summary>
/// The database does not match the EF model, so the application refuses to start instead of
/// failing later on the first query that touches the missing table or column.
///
/// Two situations produce it, and both mean the migrations on disk are not the ones that built
/// the database:
///   - a schema was "baselined" (existing tables adopted, initial migration marked as applied)
///     even though those tables are older than the current model. This is what happens when a
///     database predates the module split, or when <c>InitialSchema</c> was edited in place after
///     it had already been applied somewhere;
///   - migrations ran cleanly but the resulting schema still lacks something the model maps.
///
/// Recovery is a decision about data, so the message stays explicit: either rebuild the database
/// from the migrations (discards data) or write the missing DDL.
/// </summary>
public sealed class SchemaMismatchException(string message) : Exception(message);
