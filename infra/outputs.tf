output "lambda_arn" {
  description = "ARN da função Lambda ClienteAuthFunction"
  value       = aws_lambda_function.auth_lambda.arn
}

output "lambda_function_name" {
  description = "Nome da função Lambda"
  value       = aws_lambda_function.auth_lambda.function_name
}

output "lambda_role_arn" {
  description = "ARN da IAM Role associada à função Lambda"
  value       = aws_iam_role.lambda_exec.arn
}
