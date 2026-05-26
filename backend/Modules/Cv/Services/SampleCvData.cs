namespace NextStep.Modules.Cv.Services;

using NextStep.Modules.Cv.Models;

public static class SampleCvData
{
    public static CvData Create() => new()
    {
        Candidate = new CvCandidate
        {
            Name = "Alex Mercer",
            Email = "alex.mercer@email.com",
            Phone = "+1 (555) 123-4567",
            Location = "San Francisco, CA",
            LinkedIn = "linkedin.com/in/alexmercer",
            GitHub = "github.com/alexmercer",
            Portfolio = "alexmercer.dev",
        },
        Summary = "Senior Full-Stack Engineer with 8+ years of experience building scalable web applications. Expert in Angular, React, Node.js, and cloud-native architectures on AWS. Passionate about developer experience, CI/CD automation, and mentoring junior engineers.",
        Experience =
        [
            new CvExperience
            {
                Role = "Full-Stack Developer",
                Company = "StartupXYZ",
                Start = "2018-06",
                End = "2021-02",
                Bullets =
                [
                    "Built real-time dashboard with React, Redux, and WebSockets for live data visualization",
                    "Developed RESTful APIs with Node.js, Express, and PostgreSQL handling 1M+ requests/day",
                    "Implemented end-to-end testing with Cypress and integration tests with Supertest, achieving 90% code coverage",
                ],
            },
        ],
        Education =
        [
            new CvEducation
            {
                Degree = "M.Sc. in Computer Science",
                Institution = "Stanford University",
                Year = string.Empty,
            },
            new CvEducation
            {
                Degree = "B.Sc. in Software Engineering",
                Institution = "UC Berkeley",
                Year = string.Empty,
            },
        ],
        Skills =
        [
            new CvSkill { Name = "Angular", Level = 5, IsMatched = true },
            new CvSkill { Name = "React", Level = 5, IsMatched = true },
            new CvSkill { Name = "Node.js", Level = 5, IsMatched = true },
            new CvSkill { Name = "TypeScript", Level = 5, IsMatched = true },
            new CvSkill { Name = "Python", Level = 4, IsMatched = true },
            new CvSkill { Name = "PostgreSQL", Level = 5, IsMatched = true },
            new CvSkill { Name = "Docker", Level = 5, IsMatched = true },
        ],
        Projects =
        [
            new CvProject
            {
                Title = "Open Source - ngx-dashboard",
                Description = "Open-source Angular component library for building analytics dashboards",
                Bullets =
                [
                    "Published on npm with 2K+ weekly downloads and 400+ GitHub stars",
                    "Built 20+ reusable components with Storybook documentation and coverage",
                ],
            },
        ],
        Certifications =
        [
            "AWS Solutions Architect - Professional",
            "Google Cloud Professional Data Engineer",
        ],
        Languages =
        [
            "English - Native",
            "French - Professional (C1)",
         
        ],
        Activities =
        [
            new CvActivity { Title = "IT Dayz" },
            new CvActivity { Title = "Public Speaker" },
        ],
        Sections =
        [
            new CvSection
            {
                Id = "activities",
                Type = CvSectionTypes.Activities,
                Title = "Activities",
                Placement = CvSectionPlacements.Sidebar,
                IsVisible = true,
                Order = 70,
                Items =
                [
                    new CvSectionItem { PrimaryText = "IDS" },
                    new CvSectionItem { PrimaryText = "ITWAVE" },
                ]
            },
            new CvSection
            {
                Id = "accomplishments",
                Type = CvSectionTypes.Accomplishments,
                Title = "Accomplishments",
                Placement = CvSectionPlacements.Sidebar,
                IsVisible = true,
                Order = 80,
                Items =
                [
                    new CvSectionItem { PrimaryText = "Built full-stack and AI-powered projects with measurable delivery outcomes" },
                    new CvSectionItem { PrimaryText = "Recognized in hackathon and team-based technical competitions" },
                ]
            }
        ],
        ThemeColor = "#1B2A4A",
        AtsScore = 94,
        MatchingScore = 91,
        AtsCoveragePct = 88.5,
    };
}
