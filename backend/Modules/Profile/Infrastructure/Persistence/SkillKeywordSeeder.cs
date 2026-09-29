using Microsoft.EntityFrameworkCore;
using NextStep.Shared.Persistence;

namespace NextStep.Modules.Profile.Infrastructure.Persistence;

/// <summary>Seeds the core skill-keyword suggestions (idempotent: only missing keywords are added).</summary>
public class SkillKeywordSeeder(ProfileDbContext db, ILogger<SkillKeywordSeeder> logger) : IModuleSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var added = await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO profile.skill_keyword (mot, categorie)
            SELECT v.mot, v.categorie
            FROM (VALUES
                -- Programming languages
                ('JavaScript', 'Technique'), ('TypeScript', 'Technique'), ('Python', 'Technique'),
                ('Java', 'Technique'), ('C#', 'Technique'), ('Go', 'Technique'), ('Rust', 'Technique'),
                ('PHP', 'Technique'), ('Kotlin', 'Technique'), ('Swift', 'Technique'),
                -- Interface / Frontend
                ('HTML', 'Interface'), ('CSS', 'Interface'), ('SASS/SCSS', 'Interface'),
                ('Tailwind CSS', 'Interface'), ('Bootstrap', 'Interface'), ('Material UI', 'Interface'),
                ('Angular', 'Interface'), ('React', 'Interface'), ('Vue.js', 'Interface'),
                ('Next.js', 'Interface'), ('Accessibility (a11y)', 'Interface'),
                ('Responsive Design', 'Interface'), ('Figma', 'Interface'),
                -- Infrastructure as Code
                ('Terraform', 'Infrastructure as Code'), ('Pulumi', 'Infrastructure as Code'),
                ('AWS CloudFormation', 'Infrastructure as Code'), ('AWS CDK', 'Infrastructure as Code'),
                ('OpenTofu', 'Infrastructure as Code'), ('Ansible', 'Infrastructure as Code'),
                ('Packer', 'Infrastructure as Code'), ('Vagrant', 'Infrastructure as Code'),
                -- DevOps tools
                ('Docker', 'DevOps'), ('Kubernetes', 'DevOps'), ('Helm', 'DevOps'),
                ('Jenkins', 'DevOps'), ('GitHub Actions', 'DevOps'), ('GitLab CI/CD', 'DevOps'),
                ('ArgoCD', 'DevOps'), ('Prometheus', 'DevOps'), ('Grafana', 'DevOps'),
                ('ELK Stack', 'DevOps'), ('SonarQube', 'DevOps'), ('Trivy', 'DevOps'),
                ('Snyk', 'DevOps'), ('Datadog', 'DevOps'), ('New Relic', 'DevOps')
,
                -- Beginner-friendly modern stack
                ('GitHub', 'Outil'), ('VS Code', 'Outil'), ('npm', 'Outil'),
                ('pnpm', 'Outil'), ('Yarn', 'Outil'),
                ('Vite', 'Interface'), ('Svelte', 'Interface'), ('SvelteKit', 'Interface'),
                ('Nuxt', 'Interface'), ('Astro', 'Interface'),
                ('Redux Toolkit', 'Interface'), ('Zustand', 'Interface'),
                ('TanStack Query', 'Interface'), ('React Router', 'Interface'),
                ('Framer Motion', 'Interface'),
                ('Node.js', 'Technique'), ('Express.js', 'Technique'),
                ('NestJS', 'Technique'), ('FastAPI', 'Technique'),
                ('Prisma', 'Base de donnees'), ('Drizzle ORM', 'Base de donnees'),
                ('Supabase', 'Base de donnees'), ('PlanetScale', 'Base de donnees'),
                ('Neon', 'Base de donnees'),
                ('Redis', 'Base de donnees'),
                ('Firebase Auth', 'Cloud'), ('Cloudflare', 'Cloud'),
                ('Vercel', 'Cloud'), ('Netlify', 'Cloud'),
                ('Playwright', 'Technique'), ('Vitest', 'Technique'),
                ('ESLint', 'Outil'), ('Prettier', 'Outil'),
                ('Docker Compose', 'DevOps'), ('GitHub Codespaces', 'DevOps'),
                ('CI/CD Pipelines', 'DevOps'),
                ('OpenAI API', 'IA'), ('Prompt Engineering', 'IA'),

                -- Full-stack frontend ecosystem
                ('Remix', 'Interface'), ('SolidJS', 'Interface'), ('Qwik', 'Interface'),
                ('Alpine.js', 'Interface'), ('HTMX', 'Interface'), ('jQuery', 'Interface'),
                ('Mantine', 'Interface'), ('Ant Design', 'Interface'), ('PrimeNG', 'Interface'),
                ('PrimeReact', 'Interface'), ('MUI X', 'Interface'),
                ('React Hook Form', 'Interface'), ('Formik', 'Interface'), ('Zod', 'Interface'),
                ('Yup', 'Interface'), ('SWR', 'Interface'), ('Apollo Client', 'Interface'),
                ('PWA', 'Interface'), ('Web Performance Optimization', 'Interface'),
                ('Internationalization (i18n)', 'Interface'), ('Design Systems', 'Interface'),

                -- Backend and API ecosystem
                ('ASP.NET Core', 'Technique'), ('Spring Boot', 'Technique'),
                ('Django', 'Technique'), ('Flask', 'Technique'), ('Laravel', 'Technique'),
                ('Ruby on Rails', 'Technique'), ('Phoenix', 'Technique'),
                ('gRPC', 'Technique'), ('tRPC', 'Technique'), ('OpenAPI/Swagger', 'Technique'),
                ('OAuth2', 'Technique'), ('OpenID Connect', 'Technique'), ('JWT', 'Technique'),
                ('Webhooks', 'Technique'), ('Rate Limiting', 'Technique'),
                ('Background Jobs', 'Technique'), ('Message Queues', 'Technique'),
                ('RabbitMQ', 'Technique'), ('ActiveMQ', 'Technique'),

                -- Data and storage
                ('MariaDB', 'Base de donnees'), ('DynamoDB', 'Base de donnees'),
                ('Cassandra', 'Base de donnees'), ('Couchbase', 'Base de donnees'),
                ('Elasticsearch', 'Base de donnees'), ('TimescaleDB', 'Base de donnees'),
                ('CockroachDB', 'Base de donnees'), ('SQL Optimization', 'Base de donnees'),
                ('Database Migrations', 'Base de donnees'),

                -- Cloud and platform
                ('AWS Lambda', 'Cloud'), ('Amazon ECS', 'Cloud'), ('Amazon EKS', 'Cloud'),
                ('Azure Functions', 'Cloud'), ('Azure DevOps', 'Cloud'),
                ('Google Cloud Run', 'Cloud'), ('Google Kubernetes Engine', 'Cloud'),
                ('Cloudflare Workers', 'Cloud'), ('Serverless Architecture', 'Cloud'),

                -- DevOps and SRE
                ('GitOps', 'DevOps'), ('CI/CD', 'DevOps'),
                ('Infrastructure Monitoring', 'DevOps'), ('Application Logging', 'DevOps'),
                ('Kustomize', 'DevOps'), ('NATS', 'DevOps'),
                ('Blue/Green Deployment', 'DevOps'), ('Canary Deployment', 'DevOps'),
                ('Incident Response', 'DevOps'), ('SLO/SLI', 'DevOps'),

                -- Testing and quality
                ('Unit Testing', 'Technique'), ('Integration Testing', 'Technique'),
                ('End-to-End Testing', 'Technique'), ('API Testing', 'Technique'),
                ('Postman Collections', 'Technique'), ('Contract Testing', 'Technique'),
                ('Test Automation', 'Technique'), ('Code Review', 'Technique'),

                -- Security
                ('OWASP Top 10', 'Security'), ('Secure Coding', 'Security'),
                ('Secrets Management', 'Security'), ('Role-Based Access Control', 'Security'),
                ('Security Testing', 'Security'), ('Dependency Scanning', 'Security'),

                -- Architecture and engineering practices
                ('Monolith', 'Architecture'), ('Microservices', 'Architecture'),
                ('Event-Driven Architecture', 'Architecture'),
                ('Domain-Driven Design', 'Architecture'),
                ('Clean Code', 'Architecture'), ('Refactoring', 'Architecture'),
                ('System Design', 'Architecture'),

                -- Mobile and cross-platform
                ('React Native', 'Framework'), ('Ionic', 'Framework'),
                ('Expo', 'Framework'), ('Capacitor', 'Framework'),

                -- AI engineering
                ('RAG', 'IA'), ('Vector Databases', 'IA'),
                ('LangChain', 'IA'), ('LlamaIndex', 'IA'),
                ('Embeddings', 'IA'), ('LLM Evaluation', 'IA')
            ) AS v(mot, categorie)
            WHERE NOT EXISTS (
                SELECT 1
                FROM profile.skill_keyword sk
                WHERE lower(sk.mot) = lower(v.mot) AND sk.categorie = v.categorie
            );
        ", ct);

        if (added > 0)
            logger.LogInformation("Profile - {Count} skill keywords seeded", added);
    }
}
