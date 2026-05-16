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
                Role = "Senior Full-Stack Engineer",
                Company = "TechCorp Inc.",
                Start = "2021-03",
                End = null,
                Bullets =
                [
                    "Architected and delivered a micro-frontend SaaS platform serving 50K+ users using Angular, Nx, and Module Federation",
                    "Designed CI/CD pipelines with GitHub Actions, Docker, and AWS ECS, reducing deployment time by 70%",
                    "Led a team of 4 engineers, conducting code reviews, sprint planning, and mentoring junior developers",
                    "Migrated legacy monolith to event-driven microservices on AWS Lambda + SQS, cutting infrastructure costs by 40%",
                ],
            },
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
                    "Containerized all services with Docker and orchestrated with docker-compose for local development",
                ],
            },
            new CvExperience
            {
                Role = "Junior Developer",
                Company = "WebAgency Co.",
                Start = "2016-09",
                End = "2018-05",
                Bullets =
                [
                    "Developed responsive landing pages and SPAs using AngularJS and Bootstrap",
                    "Created REST API documentation with Swagger/OpenAPI for client-facing integrations",
                    "Automated database migrations and backups with Python scripts and cron jobs",
                ],
            },
        ],
        Education =
        [
            new CvEducation
            {
                Degree = "M.Sc. in Computer Science",
                Institution = "Stanford University",
                Year = "2016",
            },
            new CvEducation
            {
                Degree = "B.Sc. in Software Engineering",
                Institution = "UC Berkeley",
                Year = "2014",
            },
        ],
        Skills =
        [
            new CvSkill { Name = "Angular", Level = 5, IsMatched = true },
            new CvSkill { Name = "React", Level = 4, IsMatched = true },
            new CvSkill { Name = "Node.js", Level = 5, IsMatched = true },
            new CvSkill { Name = "TypeScript", Level = 5, IsMatched = true },
            new CvSkill { Name = "PostgreSQL", Level = 4, IsMatched = true },
            new CvSkill { Name = "Docker", Level = 4, IsMatched = false },
            new CvSkill { Name = "AWS", Level = 4, IsMatched = false },
            new CvSkill { Name = "Python", Level = 3, IsMatched = false },
            new CvSkill { Name = "GraphQL", Level = 3, IsMatched = false },
            new CvSkill { Name = "Redis", Level = 3, IsMatched = false },
        ],
        Projects =
        [
            new CvProject
            {
                Title = "Open Source — ngx-dashboard",
                Description = "Open-source Angular component library for building analytics dashboards",
                Bullets =
                [
                    "Published on npm with 2K+ weekly downloads and 400+ GitHub stars",
                    "Built 20+ reusable components with全面的 Storybook documentation and coverage",
                ],
            },
            new CvProject
            {
                Title = "E-Commerce Platform",
                Description = "Full-stack e-commerce solution with real-time inventory management",
                Bullets =
                [
                    "Architected microservices with Node.js, RabbitMQ, and MongoDB for product catalog, orders, and payments",
                    "Integrated Stripe Connect for marketplace payments and Plaid for identity verification",
                ],
            },
        ],
        Certifications =
        [
            "AWS Solutions Architect — Professional",
            "Google Cloud Professional Data Engineer",
            "Certified Kubernetes Administrator (CKA)",
        ],
        Languages =
        [
            "English — Native",
            "French — Professional (C1)",
            "Spanish — Conversational (B1)",
        ],
        Activities =
        [
            new CvActivity
            {
                Title = "Tech Meetup Organizer",
                Role = "Co-Organizer",
                Description = "Co-organizing a local Angular meetup with 300+ members, monthly talks, and workshops",
            },
            new CvActivity
            {
                Title = "Open Source Contributor",
                Role = "Contributor",
                Description = "Active contributor to Angular Material and Nx workspaces projects",
            },
        ],
        ThemeColor = "#1B2A4A",
        FontFamily = "Inter",
        AtsScore = 94,
        MatchingScore = 91,
        AtsCoveragePct = 88.5,
    };
}
