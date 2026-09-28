-- ============================================================
-- 01 — Reference data: skill keywords used for autocomplete
-- Idempotent: rows already present (case-insensitive on mot) are skipped.
-- The backend seeds a core subset at startup; this adds the full list.
-- ============================================================

INSERT INTO profile.skill_keyword (mot, categorie)
SELECT v.mot, v.categorie
FROM (VALUES
    -- Languages
    ('JavaScript', 'Technique'), ('TypeScript', 'Technique'), ('Python', 'Technique'),
    ('Java', 'Technique'), ('C#', 'Technique'), ('C++', 'Technique'),
    ('PHP', 'Technique'), ('Go', 'Technique'), ('Rust', 'Technique'),
    ('Ruby', 'Technique'), ('Swift', 'Technique'), ('Kotlin', 'Technique'),
    ('HTML', 'Technique'), ('CSS', 'Technique'), ('SASS/SCSS', 'Technique'),
    ('Tailwind CSS', 'Technique'), ('Bootstrap', 'Technique'),
    ('REST API', 'Technique'), ('GraphQL', 'Technique'), ('WebSocket', 'Technique'),
    -- Frameworks
    ('Angular', 'Framework'), ('React', 'Framework'), ('Vue.js', 'Framework'),
    ('Next.js', 'Framework'), ('Node.js', 'Framework'), ('Express.js', 'Framework'),
    ('NestJS', 'Framework'), ('Spring Boot', 'Framework'), ('.NET', 'Framework'),
    ('ASP.NET Core', 'Framework'), ('Django', 'Framework'), ('Flask', 'Framework'),
    ('FastAPI', 'Framework'), ('Laravel', 'Framework'), ('Ruby on Rails', 'Framework'),
    -- Databases
    ('PostgreSQL', 'Base de données'), ('MySQL', 'Base de données'), ('MongoDB', 'Base de données'),
    ('Redis', 'Base de données'), ('SQLite', 'Base de données'), ('SQL Server', 'Base de données'),
    ('Oracle', 'Base de données'), ('Firebase', 'Base de données'),
    -- DevOps
    ('Docker', 'DevOps'), ('Kubernetes', 'DevOps'), ('Jenkins', 'DevOps'),
    ('GitHub Actions', 'DevOps'), ('GitLab CI/CD', 'DevOps'), ('Terraform', 'DevOps'),
    ('Ansible', 'DevOps'), ('Helm', 'DevOps'), ('ArgoCD', 'DevOps'),
    ('Prometheus', 'DevOps'), ('Grafana', 'DevOps'), ('SonarQube', 'DevOps'),
    ('ELK Stack', 'DevOps'), ('Datadog', 'DevOps'), ('New Relic', 'DevOps'),
    ('Snyk', 'DevOps'), ('Trivy', 'DevOps'), ('Vault', 'DevOps'),
    ('Consul', 'DevOps'), ('Istio', 'DevOps'), ('Linkerd', 'DevOps'),
    -- Cloud
    ('AWS', 'Cloud'), ('Azure', 'Cloud'), ('Google Cloud', 'Cloud'),
    -- Tools
    ('Git', 'Outil'), ('Linux', 'Outil'), ('Nginx', 'Outil'), ('Jira', 'Outil'),
    ('Figma', 'Outil'), ('Postman', 'Outil'),
    -- Architecture
    ('Microservices', 'Architecture'), ('Design Patterns', 'Architecture'),
    ('Clean Architecture', 'Architecture'), ('SOLID', 'Architecture'),
    -- Methodology
    ('Agile/Scrum', 'Méthodologie'), ('Kanban', 'Méthodologie'),
    ('TDD', 'Méthodologie'), ('CI/CD', 'Méthodologie'),
    -- AI
    ('Machine Learning', 'IA'), ('Deep Learning', 'IA'), ('NLP', 'IA'),
    ('TensorFlow', 'IA'), ('PyTorch', 'IA'), ('scikit-learn', 'IA'),
    -- Soft skills
    ('Communication', 'Soft Skill'), ('Travail en équipe', 'Soft Skill'),
    ('Leadership', 'Soft Skill'), ('Résolution de problèmes', 'Soft Skill'),
    ('Gestion du temps', 'Soft Skill'), ('Adaptabilité', 'Soft Skill'), ('Créativité', 'Soft Skill')
) AS v(mot, categorie)
WHERE NOT EXISTS (
    SELECT 1 FROM profile.skill_keyword sk
    WHERE lower(sk.mot) = lower(v.mot) AND sk.categorie = v.categorie
);
