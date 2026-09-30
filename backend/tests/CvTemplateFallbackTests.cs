using FluentAssertions;
using NextStep.Modules.CvDocuments.Domain;
using QuestPDF.Fluent;
using NextStep.Modules.CvDocuments.Infrastructure.Rendering;
using NextStep.Modules.CvDocuments.Templates;
using QuestPDF.Infrastructure;
using Xunit;

namespace NextStep.Tests;

/// <summary>
/// Puppeteer is the primary renderer and QuestPDF is the fallback, so every template
/// the HTML renderer accepts must have a QuestPDF document behind it. This guards the
/// fallback: a missing one turns a rendering hiccup into a 500 for that template only.
/// </summary>
public class CvTemplateFallbackTests
{
    private static CvData Sample() => new()
    {
        Candidate = new CvCandidate
        {
            Name = "Sara Alami", Email = "sara@example.com",
            Phone = "+212600000000", Location = "Casablanca, Morocco"
        },
        Summary = "Backend engineer.",
        Experience =
        {
            new CvExperience { Role = "Developer", Company = "Acme", Start = "2022", End = "Present", Bullets = { "Shipped services." } }
        },
        Education = { new CvEducation { Degree = "Master", Institution = "ENSIAS", Year = "2021" } },
        Skills = { new CvSkill { Name = "C#", Level = 4, IsMatched = true } },
        Languages = { "French", "English" }
    };

    [Fact]
    public void Every_html_template_has_a_questpdf_fallback()
    {
        var missing = CvHtmlTemplateRenderer.TemplateSlugs
            .Where(slug => !QuestPdfTemplates.Covers(slug))
            .ToList();

        missing.Should().BeEmpty(
            "a template accepted by the HTML renderer must also render on the QuestPDF fallback path");
    }

    [Fact]
    public void Tech_latex_aliases_resolve_to_the_latex_document()
    {
        QuestPdfTemplates.Covers("tech-latex").Should().BeTrue();
        QuestPdfTemplates.Covers("tech_latex").Should().BeTrue();
    }

    [Theory]
    [InlineData("modern")]
    [InlineData("latex")]
    [InlineData("executive")]
    [InlineData("horizon")]
    public void The_fallback_document_actually_generates_a_pdf(string slug)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var act = () => CvDocumentFactory.Create(slug, Sample());

        act.Should().NotThrow();
        var pdf = act().GeneratePdf();
        pdf.Should().HaveCountGreaterThan(500);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public void An_unknown_template_is_rejected_with_the_supported_list()
    {
        var act = () => CvDocumentFactory.Create("does-not-exist", Sample());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*does-not-exist*");
    }
}
