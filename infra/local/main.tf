terraform {
  required_version = ">= 1.0.0"
}

# Orchestrator: Launch and manage Vagrant VMs
resource "null_resource" "vagrant_vms" {
  # Trigger re-launch only if the Vagrantfile changes
  triggers = {
    vagrantfile_hash = filemd5("${path.module}/Vagrantfile")
  }

  # Action: Launch VMs
  provisioner "local-exec" {
    command = "vagrant up --provider vmware_desktop"
    working_dir = path.module
  }

  # Action: Destroy VMs on 'terraform destroy'
  provisioner "local-exec" {
    when    = destroy
    command = "vagrant destroy -f"
    working_dir = path.module
  }
}
