locals {
  repositories = ["api", "worker", "nginx"]
}

resource "aws_ecr_repository" "main" {
  for_each             = toset(local.repositories)
  name                 = "${var.project_name}/${var.environment}/${each.key}"
  image_tag_mutability = "IMMUTABLE"
  force_delete         = false
  tags = merge(var.tags, {
    Name    = "${var.project_name}-${var.environment}-${each.key}"
    Service = each.key
  })
}

resource "aws_ecr_repository_policy" "main" {
  for_each   = aws_ecr_repository.main
  repository = each.value.name
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowPushPull"
        Effect = "Allow"
        Principal = {
          AWS = var.allowed_principals
        }
        Action = [
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:BatchCheckLayerAvailability",
          "ecr:PutImage",
          "ecr:InitiateLayerUpload",
          "ecr:UploadLayerPart",
          "ecr:CompleteLayerUpload"
        ]
      }
    ]
  })
}

resource "aws_ecr_lifecycle_policy" "main" {
  for_each   = aws_ecr_repository.main
  repository = each.value.name
  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 10 images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 10
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}

resource "aws_ecr_repository_policy" "ecr_scan" {
  for_each   = aws_ecr_repository.main
  repository = each.value.name
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "EnableScanning"
        Effect = "Allow"
        Principal = {
          Service = "ecr.amazonaws.com"
        }
        Action = "ecr:StartImageScan"
      }
    ]
  })
}
