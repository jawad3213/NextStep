using System.Text.Json.Serialization;

namespace NextStep.Modules.Identity.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ObjectifEnum
    {
        STAGE,
        ALTERNANCE,
        PREMIER_EMPLOI,
        CDI,
        FREELANCE
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum NiveauEnum
    {
        BAC,
        BAC_PLUS_2,
        BAC_PLUS_3,
        BAC_PLUS_5,
        DOCTORAT
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SecteurEnum
    {
        INFORMATIQUE,
        FINANCE,
        MARKETING,
        SANTE,
        INGENIERIE,
        DROIT,
        EDUCATION,
        COMMERCE,
        DESIGN,
        COMMUNICATION,
        RESSOURCES_HUMAINES,
        AUTRE
    }
}
