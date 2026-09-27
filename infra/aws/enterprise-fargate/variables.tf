variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "nextstep"
}

variable "environment" {
  description = "Deployment environment (e.g. production, staging)"
  type        = string
  default     = "production"
}

variable "aws_region" {
  description = "AWS region for all resources"
  type        = string
  default     = "us-east-1"
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "availability_zones" {
  description = "List of availability zones (auto-selected if empty)"
  type        = list(string)
  default     = []
}

variable "db_instance_class" {
  description = "RDS instance type"
  type        = string
  default     = "db.t3.medium"
}

variable "db_engine" {
  description = "RDS database engine"
  type        = string
  default     = "postgres"
}

variable "db_engine_version" {
  description = "RDS engine version"
  type        = string
  default     = "15"
}

variable "db_storage_gb" {
  description = "RDS allocated storage in GB"
  type        = number
  default     = 100
}

variable "fargate_cpu" {
  description = "Fargate task CPU units (512 = 0.5 vCPU)"
  type        = number
  default     = 512
}

variable "fargate_memory" {
  description = "Fargate task memory in MB"
  type        = number
  default     = 1024
}

variable "fargate_desired_count" {
  description = "Desired Fargate task count"
  type        = number
  default     = 2
}

variable "container_port" {
  description = "Application container port"
  type        = number
  default     = 8080
}

variable "alarm_email" {
  description = "Email address for CloudWatch alarm notifications"
  type        = string
  default     = ""
}

variable "tags" {
  description = "Common resource tags"
  type        = map(string)
  default     = {}
}
