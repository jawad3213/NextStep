output "dashboard_name" {
  description = "CloudWatch dashboard name"
  value       = aws_cloudwatch_dashboard.main.dashboard_name
}

output "alarm_arns" {
  description = "Map of alarm ARNs by name"
  value = {
    fargate-cpu     = try(aws_cloudwatch_metric_alarm.fargate_cpu[0].arn, "")
    fargate-memory  = try(aws_cloudwatch_metric_alarm.fargate_memory[0].arn, "")
    alb-5xx         = try(aws_cloudwatch_metric_alarm.alb_5xx[0].arn, "")
    rds-cpu         = try(aws_cloudwatch_metric_alarm.rds_cpu[0].arn, "")
    rds-connections = try(aws_cloudwatch_metric_alarm.rds_connections[0].arn, "")
    rds-storage     = try(aws_cloudwatch_metric_alarm.rds_storage[0].arn, "")
  }
}

output "log_group_names" {
  description = "Map of log group names"
  value = {
    for k, lg in aws_cloudwatch_log_group.main : k => lg.name
  }
}

output "sns_topic_arn" {
  description = "SNS topic ARN for alarms"
  value       = aws_sns_topic.alarms.arn
}
