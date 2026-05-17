variable "project_name" {
  description = "Project name for resource naming"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "aws_region" {
  description = "AWS region"
  type        = string
}

variable "log_retention_days" {
  description = "CloudWatch log retention in days"
  type        = number
  default     = 30
}

variable "log_groups" {
  description = "List of CloudWatch log group names to create"
  type        = list(string)
  default     = []
}

variable "kms_key_arn" {
  description = "KMS key ARN for log encryption"
  type        = string
  default     = ""
}

variable "alarm_email" {
  description = "Email address for alarm notifications"
  type        = string
  default     = ""
}

variable "ecs_cluster_name" {
  description = "ECS cluster name for Fargate metrics"
  type        = string
  default     = ""
}

variable "ecs_service_name" {
  description = "ECS service name for Fargate metrics"
  type        = string
  default     = ""
}

variable "alb_arn" {
  description = "ALB ARN for load balancer metrics"
  type        = string
  default     = ""
}

variable "alb_name" {
  description = "ALB name for CloudWatch dimensions"
  type        = string
  default     = ""
}

variable "db_instance_identifier" {
  description = "RDS instance identifier for database metrics"
  type        = string
  default     = ""
}

variable "tags" {
  description = "Common resource tags"
  type        = map(string)
  default     = {}
}
