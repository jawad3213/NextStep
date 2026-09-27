variable "project_name" {
  description = "Project name for resource naming"
  type        = string
}

variable "environment" {
  description = "Deployment environment"
  type        = string
}

variable "vpc_id" {
  description = "VPC ID for security group placement"
  type        = string
}

variable "container_port" {
  description = "Application container port for security group rules"
  type        = number
  default     = 8080
}

variable "tags" {
  description = "Common resource tags"
  type        = map(string)
  default     = {}
}
