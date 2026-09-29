#!/bin/bash
set -euo pipefail

# Bootstrap Terraform state backend
# Creates S3 bucket for state files and DynamoDB table for state locking

REGION="${AWS_REGION:-us-east-1}"
BUCKET="${TF_STATE_BUCKET:-nextstep-terraform-state}"
TABLE="${TF_LOCK_TABLE:-nextstep-terraform-locks}"

echo "Creating S3 state bucket: $BUCKET"
aws s3 mb "s3://$BUCKET" --region "$REGION"
aws s3api put-bucket-versioning \
  --bucket "$BUCKET" \
  --versioning-configuration Status=Enabled
aws s3api put-bucket-encryption \
  --bucket "$BUCKET" \
  --server-side-encryption-configuration '{"Rules":[{"ApplyServerSideEncryptionByDefault":{"SSEAlgorithm":"AES256"}}]}'

echo "Creating DynamoDB lock table: $TABLE"
aws dynamodb create-table \
  --table-name "$TABLE" \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region "$REGION"

echo "Waiting for DynamoDB table to be active..."
aws dynamodb wait table-exists --table-name "$TABLE" --region "$REGION"

echo "Bootstrap complete!"
echo "State bucket: s3://$BUCKET"
echo "Lock table: $TABLE"
