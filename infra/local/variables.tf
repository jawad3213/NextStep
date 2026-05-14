variable "vm1_ip" {
  description = "IP address for the application VM"
  type        = string
  default     = "192.168.77.10"
}

variable "vm2_ip" {
  description = "IP address for the data/monitoring VM"
  type        = string
  default     = "192.168.77.11"
}

variable "ansible_inventory_path" {
  description = "Path to the ansible inventory file"
  type        = string
  default     = "ansible/inventory.ini"
}

variable "ansible_playbook_path" {
  description = "Path to the main ansible playbook"
  type        = string
  default     = "ansible/site.yml"
}
