output "frontend_url" {
  value       = "http://${var.vm1_ip}"
  description = "Public URL to access the NextStep Frontend"
}

output "backend_api_url" {
  value       = "http://${var.vm1_ip}/api"
  description = "URL to access the Backend API"
}

output "sonarqube_url" {
  value       = "http://${var.vm2_ip}:9005"
  description = "URL to access the SonarQube Dashboard"
}

output "grafana_url" {
  value       = "http://${var.vm2_ip}:3000"
  description = "URL to access the Grafana Dashboard"
}
