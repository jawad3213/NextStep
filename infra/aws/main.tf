# ============================================================
# NextStep AWS Private Cloud — Root Module
# ============================================================

locals {
  db_identifier = module.database.db_name
}

module "networking" {
  source = "./modules/networking"

  vpc_cidr           = var.vpc_cidr
  availability_zones = var.availability_zones
  project_name       = var.project_name
  environment        = var.environment
  aws_region         = var.aws_region
  tags               = var.tags
}

module "security" {
  source = "./modules/security"

  project_name    = var.project_name
  environment     = var.environment
  vpc_id          = module.networking.vpc_id
  container_port  = var.container_port
  tags            = var.tags
}

module "storage" {
  source = "./modules/storage"

  vpc_id       = module.networking.vpc_id
  project_name = var.project_name
  environment  = var.environment
  tags         = var.tags
}

module "database" {
  source = "./modules/database"

  vpc_id                = module.networking.vpc_id
  private_subnet_ids    = module.networking.private_subnet_ids
  instance_class        = var.db_instance_class
  engine                = var.db_engine
  engine_version        = var.db_engine_version
  allocated_storage     = var.db_storage_gb
  kms_key_arn           = module.security.kms_key_arn
  vpc_security_group_id = module.security.security_group_ids["rds"]
  monitoring_role_arn   = module.security.iam_role_arns["rds-monitoring"]
  project_name          = var.project_name
  environment           = var.environment
  tags                  = var.tags
}

module "container_registry" {
  source = "./modules/container-registry"

  project_name = var.project_name
  environment  = var.environment
  tags         = var.tags
}

module "load_balancing" {
  source = "./modules/load-balancing"

  vpc_id            = module.networking.vpc_id
  subnet_ids        = module.networking.public_subnet_ids
  security_group_id = module.security.security_group_ids["alb"]
  container_port    = var.container_port
  project_name      = var.project_name
  environment       = var.environment
  tags              = var.tags
}

module "compute" {
  source = "./modules/compute"

  project_name           = var.project_name
  environment            = var.environment
  aws_region             = var.aws_region
  task_cpu               = var.fargate_cpu
  task_memory            = var.fargate_memory
  desired_count          = var.fargate_desired_count
  container_image        = module.container_registry.repository_urls["api"]
  container_port         = var.container_port
  subnet_ids             = module.networking.private_subnet_ids
  task_security_group_id = module.security.security_group_ids["fargate"]
  target_group_arn       = module.load_balancing.target_group_arn
  execution_role_arn     = module.security.iam_role_arns["fargate-execution"]
  task_role_arn          = module.security.iam_role_arns["fargate-task"]
  kms_key_arn            = module.security.kms_key_arn
  db_host                = split(":", module.database.db_endpoint)[0]
  db_port                = try(tonumber(split(":", module.database.db_endpoint)[1]), 5432)
  db_name                = module.database.db_name
  tags                   = var.tags
}

module "monitoring" {
  source = "./modules/monitoring"

  project_name           = var.project_name
  environment            = var.environment
  aws_region             = var.aws_region
  log_groups             = ["/ecs/${var.project_name}-${var.environment}", "/rds/${var.project_name}-${var.environment}"]
  kms_key_arn            = module.security.kms_key_arn
  alarm_email           = var.alarm_email
  ecs_cluster_name      = module.compute.cluster_name
  ecs_service_name      = module.compute.service_name
  alb_name              = module.load_balancing.alb_name
  db_instance_identifier = module.database.db_name
  tags                   = var.tags
}
