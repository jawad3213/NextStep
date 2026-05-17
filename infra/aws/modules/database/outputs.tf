output "db_endpoint" {
  description = "RDS endpoint (host:port)"
  value       = aws_db_instance.main.endpoint
}

output "db_port" {
  description = "RDS port"
  value       = aws_db_instance.main.port
}

output "db_name" {
  description = "Database name"
  value       = aws_db_instance.main.db_name
}

output "db_arn" {
  description = "RDS ARN"
  value       = aws_db_instance.main.arn
}

output "db_master_username" {
  description = "Database master username"
  value       = aws_db_instance.main.username
}

output "db_master_password" {
  description = "Database master password"
  value       = random_password.master.result
  sensitive   = true
}
