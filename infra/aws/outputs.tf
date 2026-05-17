output "vpc_id" {
  description = "VPC identifier"
  value       = module.networking.vpc_id
}

output "public_subnet_ids" {
  description = "Public subnet IDs"
  value       = module.networking.public_subnet_ids
}

output "private_subnet_ids" {
  description = "Private subnet IDs"
  value       = module.networking.private_subnet_ids
}

output "s3_bucket_ids" {
  description = "Map of S3 bucket IDs by purpose"
  value       = module.storage.bucket_ids
}

output "s3_bucket_arns" {
  description = "Map of S3 bucket ARNs by purpose"
  value       = module.storage.bucket_arns
}

output "db_endpoint" {
  description = "RDS endpoint (host:port)"
  value       = module.database.db_endpoint
}

output "db_name" {
  description = "RDS database name"
  value       = module.database.db_name
}

output "db_master_username" {
  description = "RDS master username"
  value       = module.database.db_master_username
}

output "ecs_cluster_id" {
  description = "ECS cluster ID"
  value       = module.compute.cluster_id
}

output "ecs_cluster_arn" {
  description = "ECS cluster ARN"
  value       = module.compute.cluster_arn
}

output "fargate_service_name" {
  description = "Fargate service name"
  value       = module.compute.service_name
}

output "alb_dns_name" {
  description = "ALB DNS name"
  value       = module.load_balancing.alb_dns_name
}

output "ecr_repository_urls" {
  description = "Map of ECR repository URLs"
  value       = module.container_registry.repository_urls
}

output "kms_key_id" {
  description = "KMS key ID"
  value       = module.security.kms_key_id
}

output "kms_key_arn" {
  description = "KMS key ARN"
  value       = module.security.kms_key_arn
}

output "iam_role_arns" {
  description = "Map of IAM role ARNs by role name"
  value       = module.security.iam_role_arns
}

output "cloudwatch_dashboard_name" {
  description = "CloudWatch dashboard name"
  value       = module.monitoring.dashboard_name
}

output "sns_topic_arn" {
  description = "SNS topic ARN for alarms"
  value       = module.monitoring.sns_topic_arn
}
