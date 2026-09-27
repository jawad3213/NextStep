resource "random_password" "master" {
  length  = 24
  special = false
}

resource "aws_db_subnet_group" "main" {
  name        = "${var.project_name}-${var.environment}"
  description = "Database subnet group for ${var.project_name}-${var.environment}"
  subnet_ids  = var.private_subnet_ids
  tags        = var.tags
}

resource "aws_db_parameter_group" "main" {
  name        = "${var.project_name}-${var.environment}-${var.engine}-${var.engine_version}"
  family      = "${var.engine}${var.engine_version}"
  description = "Parameter group for ${var.project_name}-${var.environment}"
  tags        = var.tags
}

resource "aws_db_instance" "main" {
  identifier                      = "${var.project_name}-${var.environment}-db"
  engine                          = var.engine
  engine_version                  = var.engine_version
  instance_class                  = var.instance_class
  allocated_storage               = var.allocated_storage
  max_allocated_storage           = 1000
  storage_type                    = "gp3"
  storage_encrypted               = true
  kms_key_id                      = var.kms_key_arn
  db_name                         = var.db_name
  username                        = var.db_username
  password                        = random_password.master.result
  port                            = 5432
  db_subnet_group_name            = aws_db_subnet_group.main.name
  vpc_security_group_ids          = [var.vpc_security_group_id]
  parameter_group_name            = aws_db_parameter_group.main.name
  multi_az                        = true
  backup_retention_period         = 30
  backup_window                   = "03:00-04:00"
  maintenance_window              = "sun:04:00-sun:05:00"
  copy_tags_to_snapshot           = true
  deletion_protection             = true
  skip_final_snapshot             = false
  final_snapshot_identifier       = "${var.project_name}-${var.environment}-db-final-${formatdate("YYYYMMDD-HHmmss", timestamp())}"
  auto_minor_version_upgrade      = true
  monitoring_interval             = 60
  monitoring_role_arn             = var.monitoring_role_arn
  enabled_cloudwatch_logs_exports = ["postgresql"]
  tags                            = var.tags
}
