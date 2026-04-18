# Guide : Déployer des VMs Légères sur VMware avec Terraform et Vagrant

Ce guide détaille pas à pas comment configurer et lancer des machines virtuelles Ubuntu minimalistes sur VMware Workstation, en orchestrant le tout avec Terraform.

## Pourquoi utiliser Terraform ET Vagrant ?
**Terraform** est l'outil standard pour l'infrastructure as code. Cependant, Terraform est nativement conçu pour les clouds (AWS, Azure) ou les environnements d'entreprise (VMware vSphere/ESXi). Pour interagir avec une version locale comme **VMware Workstation**, la méthode officielle et la plus stable d'HashiCorp est de s'appuyer sur **Vagrant**. 
Terraform ordonne à Vagrant (via un *provider*) de récupérer l'image et d'installer les VMs sur votre ordinateur.

---

## Étape 1 : Les Prérequis
Vous devez disposer des éléments suivants installés sur votre machine hôte Windows :

1. **VMware Workstation Pro** ou **Player** : Le moteur hyperviseur.
2. **Vagrant** : L'outil pour télécharger et configurer des images légères ("boxes").
3. **Vagrant VMware Utility** : Un service système essentiel pour que Vagrant ait les droits de créer des ressources et réseaux dans VMware. [Lien de téléchargement officiel](https://developer.hashicorp.com/vagrant/docs/providers/vmware/vagrant-vmware-utility).

---

## Étape 2 : L'Installation du Plugin VMware (IMPORTANT)
Même si Vagrant et VMware sont installés, ils ne se parlent pas par défaut. Il faut ajouter "l'adaptateur" officiel.

Ouvrez un terminal Windows (`cmd` ou `powershell`), et tapez :
```bash
vagrant plugin install vagrant-vmware-desktop
```
*Si vous obtenez une erreur indiquant que "vagrant" n'est pas reconnu, c'est que votre terminal est ouvert depuis trop longtemps. Fermez votre éditeur (VS Code), ouvrez un nouveau terminal fraîchement et recommencez.*

---

## Étape 3 : Configuration du Réseau (Adapters)
Dans le fichier `Vagrantfile` généré, nous définissons des adresses IP fixes pour vos VMs :
- `192.168.56.11` (Node 1)
- `192.168.56.12` (Node 2)

### Comment ça marche avec VMware ?
Le paramètre `private_network` indique à VMware de configurer une carte réseau **Host-Only** (Réseau privé hôte). 
1. **Routage Automatique** : Normalement, le plugin `vagrant-vmware-desktop` va vérifier vos réseaux virtuels existants (VMnet). S'il ne trouve aucun réseau sur la plage "192.168.56.x", il va **créer automatiquement** un nouveau réseau virtuel caché (par exemple `VMnet2` ou `vmnet3`) pour connecter vos machines entre elles et à votre PC.
2. **Vérification Manuelle (Si problème)** : Ouvrez `Virtual Network Editor` (Éditeur de réseau virtuel) fourni avec VMware Workstation. Vérifiez qu'il existe un réseau **Host-Only** configuré sur le sous-réseau `192.168.56.0` (masque de sous-réseau `255.255.255.0`). Si ce n'est pas le cas, vous pouvez modifier (Add Network) `VMnet1` ou un autre réseau Host-Only pour utiliser spécifiquement ce bloc d'IP.

---

## Étape 4 : Déploiement via Terraform
Maintenant que les outils sont interconnectés, vous pouvez tout piloter via Terraform.
Allez dans le dossier du projet via le terminal de VS Code :

```bash
cd infra/local/
```

Initialisez le plugin Terraform (`bmatcuk/vagrant`) :
```bash
terraform init
```

Lancez la création des machines virtuelles :
```bash
terraform apply
```
Répondez `yes` lorsqu'il vous demande validation. 

### Que va-t-il se passer sous le capot ?
1. Terraform lit votre fichier `main.tf`.
2. Il demande au *provider* Vagrant de scruter le fichier `Vagrantfile`.
3. Vagrant comprend qu'il doit télécharger l'image ultra-légère `bento/ubuntu-24.04` (si c'est la première fois).
4. Il communique avec `Vagrant VMware Utility` pour configurer le réseau.
5. Les deux machines apparaissent dans VMware et démarrent silencieusement en arrière-plan.

---

## Étape 5 : Se connecter aux VMs
Une fois le `terraform apply` terminé, pour administrer une VM, vous pouvez soit utiliser le terminal directement via Vagrant :

```bash
vagrant ssh node-01
```
Ou alors vous y connecter via un client SSH classique (ex: PuTTY ou VS Code Remote) depuis sa nouvelle IP (192.168.56.11), avec un identifiant/mot de passe par défaut qui est bien souvent `vagrant`/`vagrant` sur ces images légères.
