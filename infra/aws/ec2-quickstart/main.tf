# ============================================================
# NextStep — EC2 quickstart (single instance, Docker Compose)
#
# 1 EC2 instance in the default VPC running docker-compose.prod.yml.
# nginx (inside compose) routes the app; Caddy in front adds HTTPS when
# a domain is set. Secrets live in SSM Parameter Store (SecureString).
# ============================================================

locals {
  name        = "${var.project_name}-quickstart"
  env_content = file(var.env_file)
  app_url     = var.domain_name != "" ? "https://${var.domain_name}" : "http://${aws_eip.this.public_ip}"
}

# --- Network: default VPC, first default subnet --------------------------

data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
  filter {
    name   = "default-for-az"
    values = ["true"]
  }
}

resource "aws_security_group" "app" {
  name        = "${local.name}-sg"
  description = "NextStep quickstart: HTTP/HTTPS in, all out"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    description = "HTTP (redirects to HTTPS when a domain is set)"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTPS"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  dynamic "ingress" {
    for_each = length(var.ssh_ingress_cidrs) > 0 ? [1] : []
    content {
      description = "SSH (restricted)"
      from_port   = 22
      to_port     = 22
      protocol    = "tcp"
      cidr_blocks = var.ssh_ingress_cidrs
    }
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# --- Secrets: .env as an encrypted SSM parameter ------------------------

resource "aws_ssm_parameter" "env" {
  name        = "/${var.project_name}/quickstart/env"
  description = "NextStep production .env (read by the instance at boot)"
  type        = "SecureString"
  tier        = length(local.env_content) > 4096 ? "Advanced" : "Standard"
  value       = sensitive(local.env_content)
}

# --- IAM: SSM Session Manager access + read the .env parameter -----------

data "aws_iam_policy_document" "assume_ec2" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "instance" {
  name               = "${local.name}-role"
  assume_role_policy = data.aws_iam_policy_document.assume_ec2.json
}

resource "aws_iam_role_policy_attachment" "ssm_core" {
  role       = aws_iam_role.instance.name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

data "aws_iam_policy_document" "read_env" {
  statement {
    actions   = ["ssm:GetParameter"]
    resources = [aws_ssm_parameter.env.arn]
  }
}

resource "aws_iam_role_policy" "read_env" {
  name   = "read-env-parameter"
  role   = aws_iam_role.instance.id
  policy = data.aws_iam_policy_document.read_env.json
}

resource "aws_iam_instance_profile" "instance" {
  name = "${local.name}-profile"
  role = aws_iam_role.instance.name
}

# --- Compute ------------------------------------------------------------

data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd-gp3/ubuntu-noble-24.04-amd64-server-*"]
  }
}

# Allocated before the instance so the boot script knows the final public IP.
resource "aws_eip" "this" {
  domain = "vpc"
  tags   = { Name = "${local.name}-ip" }
}

resource "aws_instance" "app" {
  ami                    = data.aws_ami.ubuntu.id
  instance_type          = var.instance_type
  subnet_id              = data.aws_subnets.default.ids[0]
  vpc_security_group_ids = [aws_security_group.app.id]
  iam_instance_profile   = aws_iam_instance_profile.instance.name
  key_name               = var.key_name

  root_block_device {
    volume_type = "gp3"
    volume_size = var.root_volume_size
    encrypted   = true
  }

  metadata_options {
    http_tokens = "required" # IMDSv2 only
  }

  user_data = templatefile("${path.module}/user-data.sh.tftpl", {
    aws_region         = var.aws_region
    env_parameter_name = aws_ssm_parameter.env.name
    repo_url           = var.repo_url
    repo_branch        = var.repo_branch
    domain_name        = var.domain_name
    letsencrypt_email  = var.letsencrypt_email
    public_ip          = aws_eip.this.public_ip
  })
  user_data_replace_on_change = true

  tags = { Name = local.name }
}

resource "aws_eip_association" "this" {
  instance_id   = aws_instance.app.id
  allocation_id = aws_eip.this.id
}
