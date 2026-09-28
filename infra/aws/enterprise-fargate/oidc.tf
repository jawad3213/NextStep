# ============================================================
# GitHub Actions OIDC Integration (Zero static AWS credentials)
# Allows GitHub Actions from the specified repository to assume
# this role to push Docker images to ECR and update ECS Fargate.
# ============================================================

data "aws_caller_identity" "current" {}

resource "aws_iam_openid_connect_provider" "github" {
  count = var.create_oidc_provider ? 1 : 0

  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = ["6938fd4d98bab03faadb97b34396831e3780aea1", "1c58a3a8518e8759bf075b76b750d4f2df264fcd"]
}

locals {
  github_oidc_provider_arn = var.create_oidc_provider ? aws_iam_openid_connect_provider.github[0].arn : "arn:aws:iam::${data.aws_caller_identity.current.account_id}:oidc-provider/token.actions.githubusercontent.com"
}

data "aws_iam_policy_document" "github_actions_fargate_assume_role" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.github_oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_repo}:*"]
    }
  }
}

resource "aws_iam_role" "github_actions_fargate_deploy" {
  name               = "${var.project_name}-${var.environment}-github-deploy-role"
  description        = "Role assumed by GitHub Actions to push to ECR and update ECS Fargate"
  assume_role_policy = data.aws_iam_policy_document.github_actions_fargate_assume_role.json
  tags               = var.tags
}

data "aws_iam_policy_document" "github_actions_fargate_deploy" {
  # 1. ECR Login (requires * on GetAuthorizationToken)
  statement {
    sid       = "ECRAuthToken"
    actions   = ["ecr:GetAuthorizationToken"]
    resources = ["*"]
  }

  # 2. ECR Image Push/Pull
  statement {
    sid = "ECRPushImages"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:GetDownloadUrlForLayer",
      "ecr:BatchGetImage",
      "ecr:PutImage",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload"
    ]
    resources = values(module.container_registry.repository_arns)
  }

  # 3. ECS Service Deployment
  statement {
    sid = "ECSDeployService"
    actions = [
      "ecs:UpdateService",
      "ecs:DescribeServices",
      "ecs:DescribeTaskDefinition",
      "ecs:RegisterTaskDefinition"
    ]
    resources = ["*"]
  }

  # 4. Pass execution & task role when updating task definitions
  statement {
    sid     = "IAMPassRoleToECS"
    actions = ["iam:PassRole"]
    resources = [
      module.security.iam_role_arns["fargate-execution"],
      module.security.iam_role_arns["fargate-task"]
    ]
  }
}

resource "aws_iam_role_policy" "github_actions_fargate" {
  name   = "github-actions-fargate-deploy"
  role   = aws_iam_role.github_actions_fargate_deploy.id
  policy = data.aws_iam_policy_document.github_actions_fargate_deploy.json
}
