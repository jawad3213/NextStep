output "repository_urls" {
  description = "Map of repository URLs by name"
  value = {
    for k, repo in aws_ecr_repository.main : k => repo.repository_url
  }
}

output "repository_arns" {
  description = "Map of repository ARNs by name"
  value = {
    for k, repo in aws_ecr_repository.main : k => repo.arn
  }
}
