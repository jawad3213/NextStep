output "app_url" {
  description = "Public URL of the application (ready ~10-15 min after apply: images are built on first boot)"
  value       = local.app_url
}

output "public_ip" {
  description = "Elastic IP of the instance (create a DNS A record to it when using domain_name)"
  value       = aws_eip.this.public_ip
}

output "instance_id" {
  value = aws_instance.app.id
}

output "connect_command" {
  description = "Open a shell on the instance without SSH"
  value       = "aws ssm start-session --region ${var.aws_region} --target ${aws_instance.app.id}"
}

output "bootstrap_log_command" {
  description = "Follow the first-boot progress (run inside the session)"
  value       = "sudo tail -f /var/log/nextstep-bootstrap.log"
}

output "github_actions_role_arn" {
  description = "IAM Role ARN to configure in GitHub Actions variables (AWS_ROLE_ARN)"
  value       = aws_iam_role.github_actions_deploy.arn
}
