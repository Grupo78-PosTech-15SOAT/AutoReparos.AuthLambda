terraform {
  required_version = ">= 1.7.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.27"
    }
  }

  backend "s3" {
    key            = "lambda-auth/terraform.tfstate"
    dynamodb_table = "autoreparos-terraform-locks"
    encrypt        = true
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "AutoReparos"
      Component   = "ClienteAuthFunction"
      Environment = var.environment
      ManagedBy   = "Terraform"
      Repository  = "Grupo78-PosTech-15SOAT/AutoReparos.AuthLambda"
      Fase        = "Fase-3"
    }
  }
}

# Auto-descoberta da VPC caso não informada explicitamente via variável
data "aws_vpc" "selected" {
  count = var.vpc_id == "" ? 1 : 0
  tags = {
    Name = "${var.environment}-vpc"
  }
}

locals {
  vpc_id = var.vpc_id != "" ? var.vpc_id : (length(data.aws_vpc.selected) > 0 ? data.aws_vpc.selected[0].id : "")
}

# Auto-descoberta das subnets privadas da VPC caso não informadas explicitamente
data "aws_subnets" "private" {
  count = length(var.private_subnet_ids) == 0 && local.vpc_id != "" ? 1 : 0
  filter {
    name   = "vpc-id"
    values = [local.vpc_id]
  }
  tags = {
    "kubernetes.io/role/internal-elb" = "1"
  }
}

locals {
  private_subnet_ids       = length(var.private_subnet_ids) > 0 ? var.private_subnet_ids : (length(data.aws_subnets.private) > 0 ? data.aws_subnets.private[0].ids : [])
  lambda_security_group_id = var.lambda_security_group_id

}

# IAM Role para Execução da Lambda
resource "aws_iam_role" "lambda_exec" {
  name = "autoreparos-auth-lambda-role-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })
}

# Anexar políticas gerenciadas para logs CloudWatch e VPC
resource "aws_iam_role_policy_attachment" "lambda_basic" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_iam_role_policy_attachment" "lambda_vpc_access" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# CloudWatch Log Group com retenção de 14 dias
resource "aws_cloudwatch_log_group" "lambda_logs" {
  name              = "/aws/lambda/autoreparos-auth-lambda-${var.environment}"
  retention_in_days = 14
}

# Função Lambda .NET 8 (Managed Runtime da AWS)
resource "aws_lambda_function" "auth_lambda" {
  function_name = "autoreparos-auth-lambda-${var.environment}"
  role          = aws_iam_role.lambda_exec.arn
  handler       = "AutoReparos.AuthLambda::AutoReparos.AuthLambda.Functions.ClienteAuthFunction::FunctionHandler"
  runtime       = "dotnet10"
  architectures = ["x86_64"]
  timeout       = 15
  memory_size   = 512

  filename         = var.lambda_zip_path != "" ? var.lambda_zip_path : null
  source_code_hash = var.lambda_zip_path != "" ? filebase64sha256(var.lambda_zip_path) : null

  environment {
    variables = {
      Cadastros__BaseUrl      = var.cadastros_base_url
      Cadastros__ChaveInterna = var.cadastros_chave_interna
      ASPNETCORE_ENVIRONMENT  = var.environment
    }
  }

  dynamic "vpc_config" {
    for_each = length(local.private_subnet_ids) > 0 && local.lambda_security_group_id != "" ? [1] : []
    content {
      subnet_ids         = local.private_subnet_ids
      security_group_ids = [local.lambda_security_group_id]
    }
  }

  depends_on = [
    aws_cloudwatch_log_group.lambda_logs,
    aws_iam_role_policy_attachment.lambda_basic,
    aws_iam_role_policy_attachment.lambda_vpc_access
  ]
}

# Permissão para o API Gateway invocar a Lambda
resource "aws_lambda_permission" "api_gw" {
  count         = var.api_gateway_execution_arn != "" ? 1 : 0
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.auth_lambda.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${var.api_gateway_execution_arn}/*/*"
}
