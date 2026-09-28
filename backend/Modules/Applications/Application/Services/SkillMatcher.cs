using System.Text.Json;
using System.Text.RegularExpressions;
using NextStep.Shared.Json;

namespace NextStep.Modules.Applications.Application.Services;

/// <summary>
/// Deterministic skill matching between an offer's required skills and the candidate profile
/// (normalisation, synonyms, token similarity). Used when the AI result has no scoring.
/// </summary>
public static class SkillMatcher
{
    public static List<string> ExtractProfileTokens(JsonElement profileData)
    {
        var bag = new List<string>();

        var comps = profileData.GetPropertyOrNull("competences");
        if (comps.HasValue && comps.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in comps.Value.EnumerateArray())
            {
                var nom = c.GetStringOrDefault("nom") ?? c.GetStringOrDefault("name");
                if (!string.IsNullOrWhiteSpace(nom)) bag.Add(nom!);
            }
        }

        var rawSkills = profileData.GetPropertyOrNull("skills");
        if (rawSkills.HasValue && rawSkills.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in rawSkills.Value.EnumerateArray())
            {
                if (s.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(s.GetString())) bag.Add(s.GetString()!);
                else if (s.ValueKind == JsonValueKind.Object)
                {
                    var n = s.GetStringOrDefault("nom") ?? s.GetStringOrDefault("name");
                    if (!string.IsNullOrWhiteSpace(n)) bag.Add(n!);
                }
            }
        }

        ExtractTextFields(profileData.GetPropertyOrNull("experiences"), new[] { "poste", "position", "titre", "description", "technologies", "missions" }, bag);
        ExtractTextFields(profileData.GetPropertyOrNull("projets"), new[] { "nom", "title", "description", "technologies", "stack", "outils" }, bag);
        ExtractTextFields(profileData.GetPropertyOrNull("projects"), new[] { "nom", "title", "description", "technologies", "stack", "outils" }, bag);

        var resume = profileData.GetStringOrDefault("resume_professionnel") ?? profileData.GetStringOrDefault("resume") ?? profileData.GetStringOrDefault("summary");
        if (!string.IsNullOrWhiteSpace(resume)) bag.Add(resume!);

        return bag.Select(NormalizeSkill).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ExtractTextFields(JsonElement? arr, string[] fields, List<string> bag)
    {
        if (!arr.HasValue || arr.Value.ValueKind != JsonValueKind.Array) return;
        foreach (var item in arr.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            foreach (var field in fields)
            {
                var value = item.GetStringOrDefault(field);
                if (!string.IsNullOrWhiteSpace(value)) bag.Add(value!);
            }
        }
    }

    private static readonly Dictionary<string, string[]> SkillSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["javascript"] = new[] { "js", "ecmascript", "node", "nodejs", "node.js" },
        ["typescript"] = new[] { "ts" },
        ["react"] = new[] { "reactjs", "react.js", "nextjs", "next.js" },
        ["angular"] = new[] { "angularjs" },
        ["vue"] = new[] { "vuejs", "vue.js", "nuxt", "nuxtjs" },
        ["python"] = new[] { "fastapi", "django", "flask" },
        ["dotnet"] = new[] { ".net", "aspnet", "asp.net", "csharp", "c#" },
        ["java"] = new[] { "spring", "springboot", "spring boot" },
        ["postgresql"] = new[] { "postgres", "psql" },
        ["mongodb"] = new[] { "mongo" },
        ["docker"] = new[] { "container", "containers", "kubernetes", "k8s" },
        ["ci/cd"] = new[] { "github actions", "gitlab ci", "jenkins", "pipeline" },
        ["ai"] = new[] { "ml", "machine learning", "llm", "nlp", "chatbot", "rag" }
    };

    public static string NormalizeSkill(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var lower = value.ToLowerInvariant();
        lower = Regex.Replace(lower, @"[^\w\s\+#\.\/-]", " ");
        lower = Regex.Replace(lower, @"\s+", " ").Trim();
        return lower;
    }

    private static bool AreSynonyms(string left, string right)
    {
        if (left == right) return true;
        foreach (var kv in SkillSynonyms)
        {
            var family = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kv.Key };
            foreach (var s in kv.Value) family.Add(NormalizeSkill(s));
            if (family.Contains(left) && family.Contains(right)) return true;
        }
        return false;
    }

    public static double ComputeSkillSimilarity(string target, string evidence)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(evidence)) return 0;
        if (target == evidence) return 1.0;
        if (AreSynonyms(target, evidence)) return 0.95;
        if (evidence.Contains(target) || target.Contains(evidence)) return 0.8;

        var targetWords = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var evidenceWords = evidence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = targetWords.Intersect(evidenceWords, StringComparer.OrdinalIgnoreCase).Count();
        if (overlap == 0) return 0;

        var jaccard = (double)overlap / Math.Max(1, targetWords.Union(evidenceWords, StringComparer.OrdinalIgnoreCase).Count());
        return Math.Min(0.78, 0.45 + jaccard * 0.5);
    }
}
