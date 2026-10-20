variable "aws_region" {
  description = "Região da AWS para deploy da Lambda"
  type        = string
  default     = "us-east-1"
}

variable "environment" {
  description = "Ambiente de execução (production, staging, development)"
  type        = string
  default     = "production"
}

variable "lambda_zip_path" {
  description = "Caminho relativo ou absoluto para o arquivo .zip gerado pelo dotnet publish"
  type        = string
  default     = ""
}

variable "vpc_id" {
  description = "ID da VPC onde a Lambda será executada. Se vazio, será auto-descoberta via tag Name"
  type        = string
  default     = ""
}

variable "private_subnet_ids" {
  description = "Lista de subnets privadas para a Lambda (acesso ao RDS sem exposição à internet)"
  type        = list(string)
  default     = []
}

variable "lambda_security_group_id" {
  description = "ID do Security Group para a Lambda. Se vazio, um novo Security Group com saída na porta 5432 será provisionado"
  type        = string
  default     = ""
}

variable "db_connection_string" {
  description = "String de conexão completa com o PostgreSQL. Se fornecida, sobrepõe as variáveis individuais de DB"
  type        = string
  default     = ""
  sensitive   = true
}

variable "db_host" {
  description = "Host do RDS PostgreSQL (endpoint DNS)"
  type        = string
  default     = ""
}

variable "db_port" {
  description = "Porta do RDS PostgreSQL"
  type        = number
  default     = 5432
}

variable "db_name" {
  description = "Nome do banco de dados relacional"
  type        = string
  default     = "autoreparos_db"
}

variable "db_username" {
  description = "Usuário do RDS PostgreSQL"
  type        = string
  default     = "autoreparos_admin"
}

variable "db_password" {
  description = "Senha do RDS PostgreSQL"
  type        = string
  default     = ""
  sensitive   = true
}

variable "jwt_secret" {
  description = "Chave secreta para assinatura dos tokens JWT dos clientes (mínimo 32 caracteres)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "jwt_expiry_hours" {
  description = "Tempo de expiração do JWT gerado em horas"
  type        = number
  default     = 1
}

variable "api_gateway_execution_arn" {
  description = "Execution ARN do API Gateway para concessão de permissão lambda:InvokeFunction"
  type        = string
  default     = ""
}
