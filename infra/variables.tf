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
  description = "Existing shared Security Group ID created by the network state"
  type        = string
  validation {
    condition     = startswith(var.lambda_security_group_id, "sg-")
    error_message = "Pass the pre-created Lambda security group ID from the network layer."
  }
}

variable "cadastros_base_url" {
  description = "URL interna do Cadastros (por exemplo, o endereço interno do serviço no cluster). A Lambda não acessa o banco: autentica o cliente por POST /internal/clientes/autenticar"
  type        = string
  default     = ""
}

variable "cadastros_chave_interna" {
  description = "Segredo enviado no cabeçalho X-Chave-Interna ao Cadastros (o mesmo Secret do K8s do Cadastros)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "api_gateway_execution_arn" {
  description = "Execution ARN do API Gateway para concessão de permissão lambda:InvokeFunction"
  type        = string
  default     = ""
}
