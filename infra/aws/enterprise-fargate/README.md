# NextStep AWS Enterprise Fargate — Staging & Production

This directory contains the production-grade, highly-available ECS Fargate architecture for NextStep.

## Environments

We support two environments using Terraform variable files in `environments/`:

| Environment | Purpose | Fargate Tasks | DB Instance | Variable File |
|---|---|---|---|---|
| **Staging** | Pre-production testing, QA | 1 task (256 CPU, 512 MB) | db.t3.micro (20 GB) | `environments/staging.tfvars` |
| **Production** | Live end-user traffic | 2 tasks (512 CPU, 1024 MB) | db.t3.medium (100 GB) | `environments/production.tfvars` |

---

## 1. Deploy Infrastructure with Terraform

### Staging
```bash
terraform init
terraform workspace select -or-create staging
terraform apply -var-file=environments/staging.tfvars
```

### Production
```bash
terraform workspace select -or-create production
terraform apply -var-file=environments/production.tfvars
```

After each `terraform apply`, Terraform outputs the IAM role ARN created for GitHub Actions:
```bash
github_actions_role_arn = "arn:aws:iam::<ACCOUNT_ID>:role/nextstep-<env>-github-deploy-role"
```

---

## 2. GitHub Actions CI/CD Pipeline

The workflow [`.github/workflows/deploy-fargate.yml`](../../.github/workflows/deploy-fargate.yml) handles both environments automatically:

* **Staging deployment:**
  * Automatically triggered on push to `dev` or `staging` branches.
  * Or manually triggered via `Actions → Deploy — Enterprise Fargate → Run workflow (staging)`.
  * Builds and pushes to `nextstep/staging/api`, `worker`, and `nginx`.
  * Deploys to `nextstep-staging-cluster` / `nextstep-staging-service`.

* **Production deployment:**
  * Automatically triggered on push to `main` branch.
  * Or manually triggered via `Actions → Deploy — Enterprise Fargate → Run workflow (production)`.
  * Builds and pushes to `nextstep/production/api`, `worker`, and `nginx`.
  * Deploys with zero downtime to `nextstep-production-cluster` / `nextstep-production-service`.

---

## 3. GitHub Repository Variables

Set in **GitHub → Settings → Secrets and variables → Actions → Variables**:

* `AWS_FARGATE_STAGING_ROLE_ARN`: Staging role ARN output from Terraform
* `AWS_FARGATE_PROD_ROLE_ARN`: Production role ARN output from Terraform
* `AWS_REGION`: e.g. `us-east-1`
