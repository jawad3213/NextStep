output "app_ip" {
  value = "${var.network_base}.${var.app_ip_suffix}"
}

output "db_ip" {
  value = "${var.network_base}.${var.db_ip_suffix}"
}

output "infrastructure_status" {
  value = "Vagrant VMs are managed by Terraform. Use 'vagrant ssh <node>' to access."
}
