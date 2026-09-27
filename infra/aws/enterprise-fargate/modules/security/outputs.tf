output "kms_key_id" {
  description = "KMS key ID"
  value       = aws_kms_key.main.key_id
}

output "kms_key_arn" {
  description = "KMS key ARN"
  value       = aws_kms_key.main.arn
}

output "iam_role_arns" {
  description = "Map of IAM role ARNs by role name"
  value = {
    fargate-execution = aws_iam_role.fargate_execution.arn
    fargate-task      = aws_iam_role.fargate_task.arn
    rds-monitoring    = aws_iam_role.rds_monitoring.arn
    ecr-pull          = aws_iam_role.ecr_pull.arn
  }
}

output "security_group_ids" {
  description = "Map of security group IDs by name"
  value = {
    alb     = aws_security_group.alb.id
    fargate = aws_security_group.fargate.id
    rds     = aws_security_group.rds.id
  }
}
