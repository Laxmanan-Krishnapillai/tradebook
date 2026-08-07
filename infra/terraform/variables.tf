variable "name_prefix" {
  type        = string
  description = "Short, globally unique prefix for Tradebook resources."
}

variable "environment" {
  type        = string
  description = "Deployment environment name."
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be dev, staging, or prod."
  }
}

variable "location" {
  type        = string
  description = "Azure region."
}

variable "api_image" {
  type        = string
  description = "Immutable API image reference to deploy."
}

variable "postgres_admin_login" {
  type        = string
  description = "Non-secret PostgreSQL administrator login."
  default     = "tradebookadmin"
}

variable "tags" {
  type        = map(string)
  description = "Tags applied to supported Azure resources."
  default     = {}
}
