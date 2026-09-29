using System.Text.Json;

namespace NextStep.Modules.Applications.Contracts;

/// <summary>
/// Port declared by Applications and implemented by the CV Documents module:
/// normalizes raw CV JSON (from the AI pipeline or the editor) before Applications
/// stores it on an application document. Keeps Applications free of any CV types.
/// </summary>
public interface ICvContentSanitizer
{
    /// <summary>Returns the sanitized CV as JSON. Accepts the CV object or a { "data": ... } wrapper.</summary>
    string Sanitize(JsonElement cvJson);
}
