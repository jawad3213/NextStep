output "bucket_ids" {
  description = "Map of bucket IDs by purpose"
  value = {
    for k, bucket in aws_s3_bucket.main : k => bucket.id
  }
}

output "bucket_arns" {
  description = "Map of bucket ARNs by purpose"
  value = {
    for k, bucket in aws_s3_bucket.main : k => bucket.arn
  }
}
