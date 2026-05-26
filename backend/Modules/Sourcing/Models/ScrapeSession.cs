using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextStep.Modules.Sourcing.Models;

[Table("scrape_session")]
public class ScrapeSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("keywords")]
    public string? Keywords { get; set; }

    [Column("location")]
    public string? Location { get; set; }

    [Column("providers_json", TypeName = "jsonb")]
    public string ProvidersJson { get; set; } = "[]";

    [Column("country_code")]
    public string? CountryCode { get; set; }

    [Column("posted_window")]
    public string? PostedWindow { get; set; }

    [Column("contract_types_json", TypeName = "jsonb")]
    public string ContractTypesJson { get; set; } = "[]";

    [Column("limit_value")]
    public int Limit { get; set; } = 20;

    [Column("result_count")]
    public int ResultCount { get; set; }

    [Column("warnings_json", TypeName = "jsonb")]
    public string WarningsJson { get; set; } = "[]";

    [Column("errors_json", TypeName = "jsonb")]
    public string ErrorsJson { get; set; } = "[]";

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
