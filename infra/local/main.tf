terraform {
  required_providers {
    vagrant = {
      source  = "bmatcuk/vagrant"
      version = "~> 4.0.0"
    }
  }
}

# Ce fichier Terraform utilise le fournisseur Vagrant pour instancier
# les VMs définies dans votre Vagrantfile local.

# Il indique à Terraform de lire le fichier Vagrantfile qui se trouve 
# dans le même dossier, et de déployer les 2 machines virtuelles.
resource "vagrant_vm" "cluster" {
  vagrantfile_dir = "."
  
  # On s'assure d'utiliser le provider vagrant-vmware-desktop
  env = {
    VAGRANT_DEFAULT_PROVIDER = "vmware_desktop"
  }
}

