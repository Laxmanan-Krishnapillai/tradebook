terraform {
  required_version = ">= 1.9.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Production supplies these values with `terraform init -backend-config=...`.
  # Keeping them out of source prevents account details entering the repository.
  backend "azurerm" {}
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "this" {
  name     = "rg-${var.name_prefix}-${var.environment}"
  location = var.location
  tags     = var.tags
}
