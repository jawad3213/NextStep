# ── Orchestration des VMs (Vagrant) ─────────────────────────────
resource "null_resource" "vagrant_up" {
  provisioner "local-exec" {
    command     = "vagrant up"
    working_dir = path.module
  }
}

# ── Déploiement de l'Infrastructure (Ansible) ────────────────────
resource "null_resource" "ansible_deploy" {
  depends_on = [null_resource.vagrant_up]

  provisioner "local-exec" {
    command     = "ansible-playbook -i ${var.ansible_inventory_path} ${var.ansible_playbook_path}"
    working_dir = path.module
  }
}
