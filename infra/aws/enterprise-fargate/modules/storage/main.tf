resource "random_id" "suffix" {
  byte_length = 4
}

locals {
  purposes = ["app-data", "logs", "backups"]
}

resource "aws_s3_bucket" "main" {
  for_each = toset(local.purposes)
  bucket   = "${var.project_name}-${var.environment}-${each.key}-${random_id.suffix.hex}"
  tags = merge(var.tags, {
    Name    = "${var.project_name}-${var.environment}-${each.key}"
    Purpose = each.key
  })
}

resource "aws_s3_bucket_versioning" "main" {
  for_each = aws_s3_bucket.main
  bucket   = each.value.id
  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "main" {
  for_each = aws_s3_bucket.main
  bucket   = each.value.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "main" {
  for_each = aws_s3_bucket.main
  bucket   = each.value.id
  rule {
    id     = "transition-to-ia"
    status = "Enabled"
    transition {
      days          = 30
      storage_class = "STANDARD_IA"
    }
    transition {
      days          = 90
      storage_class = "GLACIER"
    }
    expiration {
      days = 365
    }
  }
}

resource "aws_s3_bucket_public_access_block" "main" {
  for_each                = aws_s3_bucket.main
  bucket                  = each.value.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_policy" "vpc_restriction" {
  for_each = aws_s3_bucket.main
  bucket   = each.value.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "RestrictToVPC"
        Effect    = "Deny"
        Principal = "*"
        Action    = "s3:*"
        Resource = [
          each.value.arn,
          "${each.value.arn}/*"
        ]
        Condition = {
          StringNotEquals = {
            "aws:SourceVpc" = var.vpc_id
          }
        }
      }
    ]
  })
}
