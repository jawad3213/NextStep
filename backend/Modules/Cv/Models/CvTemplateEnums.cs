namespace NextStep.Modules.Cv.Models;

/// <summary>
/// Industry sectors for CV template recommendations.
/// </summary>
public enum CvIndustry
{
    AdministrativeAndOffice,
    BusinessAndManagement,
    CreativeAndDesign,
    CustomerServiceAndRetail,
    EducationAndAcademic,
    FinanceAndAccounting,
    FoodServiceAndHospitality,
    HealthcareAndMedical,
    ITAndEngineering,
    MarketingAndSales,
    Other
}

/// <summary>
/// Career experience level for template targeting.
/// </summary>
public enum CvExperienceLevel
{
    StudentEntryLevel,
    MidLevel,
    SeniorExecutive
}

/// <summary>
/// Visual style of the CV template.
/// </summary>
public enum CvTemplateStyle
{
    Corporate,
    Creative,
    Elegant,
    Modern,
    Professional,
    Simple,
    Traditional
}

/// <summary>
/// Structural layout of the CV template.
/// </summary>
[Flags]
public enum CvTemplateLayout
{
    None        = 0,
    OneColumn   = 1 << 0,
    TwoColumn   = 1 << 1,
    WithPhoto   = 1 << 2,
    WithoutPhoto = 1 << 3,
    OnePage     = 1 << 4,
    TwoPage     = 1 << 5
}
