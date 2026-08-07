variable "name_prefix" {
  type        = string
  description = "Short, globally unique prefix for Tradebook resources."
  validation {
    condition = (
      can(regex("^[a-z0-9][a-z0-9-]{1,11}[a-z0-9]$", var.name_prefix)) &&
      !strcontains(var.name_prefix, "--")
    )
    error_message = "name_prefix must be 3-13 lowercase alphanumeric or single-hyphen characters and start/end alphanumeric."
  }
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
  validation {
    condition     = can(regex("@sha256:[0-9a-f]{64}$", var.api_image))
    error_message = "api_image must be pinned by sha256 digest."
  }
}

variable "database_ops_image" {
  type        = string
  description = "Immutable PostgreSQL migration/backup/restore image reference."
  validation {
    condition     = can(regex("@sha256:[0-9a-f]{64}$", var.database_ops_image))
    error_message = "database_ops_image must be pinned by sha256 digest."
  }
}

variable "secret_version" {
  type        = number
  description = "Increment to rotate the generated PostgreSQL and JWT secrets."
  default     = 1
  validation {
    condition     = var.secret_version >= 1 && floor(var.secret_version) == var.secret_version
    error_message = "secret_version must be a positive integer."
  }
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

variable "alert_email" {
  type        = string
  description = "Operations email receiver for Azure Monitor alerts; required in production."
  default     = null
  nullable    = true
  validation {
    condition = (
      (var.environment != "prod" || var.alert_email != null) &&
      (var.alert_email == null || can(regex("^[^@ ]+@[^@ ]+\\.[^@ ]+$", var.alert_email)))
    )
    error_message = "alert_email must be a valid email address and is required when environment is prod."
  }
}
