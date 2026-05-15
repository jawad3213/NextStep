terraform {
  backend "s3" {
    bucket         = "nextstep-terraform-state"
    key            = "aws/terraform.tfstate"
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "nextstep-terraform-locks"
  }
}
