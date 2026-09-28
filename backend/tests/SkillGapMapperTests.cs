using System.Text.Json;
using FluentAssertions;
using NextStep.Modules.Applications.Application.Dtos;
using NextStep.Modules.Applications.Application.Mappings;
using NextStep.Modules.Profile.Contracts;
using Xunit;

namespace NextStep.Tests;

/// <summary>The Skill Gap page gets the agents' match translated into its typed result.</summary>
public class SkillGapMapperTests
{
    private static readonly DateTime Today = new(2026, 1, 1);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    private static readonly JsonElement Offer = Json("""
        {
          "titre": "Développeur .NET",
          "competences_requises": ["C#", "Docker", "PostgreSQL"],
          "competences_souhaitees": ["Kubernetes"],
          "annees_experience": "2+"
        }
        """);

    private static CandidateProfile Candidate(params ExperienceSummary[] experiences) => new(
        new UserIdentity(Guid.NewGuid(), "Sara", "Alami", "sara@example.com"),
        Phone: null, CurrentTitle: null, Skills: [], Experiences: experiences,
        Education: [], Projects: [], Certifications: []);

    [Fact]
    public void Maps_the_agents_match_to_the_page_contract()
    {
        var match = Json("""
            {
              "score_matching": 72,
              "matched_skills": ["C#", "PostgreSQL"],
              "missing_skills": ["Kubernetes", "Docker"],
              "partial_skills": ["Azure"],
              "keywords_manquants": ["CI/CD"],
              "recommandations": ["Texte Kubernetes", "Texte Docker"],
              "required_certs": []
            }
            """);
        var candidate = Candidate(new ExperienceSummary("Dev", "Acme", new DateTime(2023, 1, 1), new DateTime(2025, 1, 1)));

        var result = SkillGapMapper.ToResult(Offer, match, candidate, Today);

        result.CandidateName.Should().Be("Sara Alami");
        result.JobTitle.Should().Be("Développeur .NET");
        result.RelevanceScore.Should().Be(72);
        result.MatchedSkills.Should().Equal(new SkillGapSkillDto("C#", "required"), new SkillGapSkillDto("PostgreSQL", "required"));
        // required skills first, and each recommendation keeps the agents' text for its skill
        result.MissingSkills.Select(s => (s.Name, s.Priority)).Should().Equal(("Docker", "high"), ("Kubernetes", "medium"));
        result.Recommendations.Select(r => (r.Title, r.Description)).Should().Equal(("Docker", "Texte Docker"), ("Kubernetes", "Texte Kubernetes"));
        result.ExperienceYears.Should().Be(2.0);
        result.RequiredYears.Should().Be(2);
        result.ExperienceGapYears.Should().Be(0);
        result.Flag.Should().Be(SkillGapFlags.Minor);
        result.CertMatch.Should().BeTrue();
        result.RevisionHints.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(90, "[]", SkillGapFlags.Perfect)]
    [InlineData(90, "[\"Docker\"]", SkillGapFlags.Minor)]
    [InlineData(55, "[]", SkillGapFlags.Critical)]
    public void Flag_depends_on_score_and_missing_required_skills(int score, string missing, string flag)
    {
        var match = Json($$"""{ "score_matching": {{score}}, "missing_skills": {{missing}} }""");
        var candidate = Candidate(new ExperienceSummary("Dev", "Acme", new DateTime(2022, 1, 1), null));

        SkillGapMapper.ToResult(Offer, match, candidate, Today).Flag.Should().Be(flag);
    }

    [Fact]
    public void Missing_years_of_experience_make_the_gap_critical()
    {
        var match = Json("""{ "score_matching": 95, "missing_skills": [] }""");

        var result = SkillGapMapper.ToResult(Offer, match, Candidate(), Today);

        result.ExperienceGapYears.Should().Be(2);
        result.Flag.Should().Be(SkillGapFlags.Critical);
    }

    [Fact]
    public void Overlapping_experiences_are_counted_once()
    {
        var years = SkillGapMapper.ExperienceYears(
        [
            new("A", "X", new DateTime(2020, 1, 1), new DateTime(2022, 1, 1)),
            new("B", "Y", new DateTime(2021, 1, 1), new DateTime(2023, 1, 1)),
            new("C", "Z", null, null),
        ], Today);

        years.Should().Be(3.0);
    }
}
