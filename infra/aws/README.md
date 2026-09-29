# AWS infrastructure — two deployment tiers

NextStep ships two Terraform stacks for AWS. They deploy the **same application**
(the images built from `backend/`, `agents/`, `frontend/`), and differ in how much
availability and isolation you pay for.

| | [`enterprise-fargate/`](enterprise-fargate/) | [`ec2-quickstart/`](ec2-quickstart/) |
|---|---|---|
| Purpose | Production blueprint | Live demo / portfolio link |
| Compute | ECS Fargate services behind an ALB | 1 EC2 instance running `docker-compose.prod.yml` |
| Database | RDS PostgreSQL, Multi-AZ, encrypted (KMS) | PostgreSQL container on the instance's encrypted disk |
| Network | Custom VPC, public/private subnets across AZs, NAT gateway | Default VPC, one security group (80/443 only) |
| Entry point | Application Load Balancer | nginx (in compose) + Caddy for Let's Encrypt HTTPS |
| Images | ECR (scan on push) | Built on the instance at first boot |
| Secrets | KMS / IAM-scoped | SSM Parameter Store SecureString, read by the instance role |
| Observability | CloudWatch alarms (Fargate CPU/mem, ALB 5xx, RDS) | Docker logs (rotated), SSM Session Manager access |
| Availability | Survives the loss of an AZ | Single instance, single AZ |
| Rough cost* | Hundreds of USD/month (NAT, ALB, RDS Multi-AZ, Fargate) | ~35 USD/month on-demand (t3.medium + 30 GB gp3 + public IPv4) |

\*us-east-1 on-demand list prices, before free tier or savings plans. Check the
[AWS Pricing Calculator](https://calculator.aws/) for your region.

## Which one to use

- **Showing the app to people** → `ec2-quickstart`. One `terraform apply`, one URL,
  and you can `terraform destroy` it when the demo is over.
- **Running it for real users** → `enterprise-fargate`. Redundant across AZs, managed
  database with backups, no single machine to patch.

Both are plain Terraform (`terraform init && terraform apply`); each folder has its own
`terraform.tfvars.example`. They do not share state and can coexist in one account.
