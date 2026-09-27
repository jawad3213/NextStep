terraform {
  required_version = ">= 1.5"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }

  # Local state on purpose: this is a single-instance demo stack.
  # terraform.tfstate contains the .env content (SSM parameter) — keep it private.
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = var.project_name
      Environment = "demo"
      ManagedBy   = "terraform"
      Stack       = "ec2-quickstart"
    }
  }
}
