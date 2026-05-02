variable "network_base" {
  description = "Base network for the VMs"
  type        = string
  default     = "192.168.99"
}

variable "app_ip_suffix" {
  description = "Suffix for the App VM IP"
  type        = string
  default     = "10"
}

variable "db_ip_suffix" {
  description = "Suffix for the DB VM IP"
  type        = string
  default     = "11"
}

# Example of a sensitive variable structure
variable "admin_password" {
  description = "Admin password (sensitive)"
  type        = string
  sensitive   = true
  default     = "change-me-locally"
}
