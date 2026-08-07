resource "azurerm_key_vault" "this" {
  name                       = "kv-${var.name_prefix}-${var.environment}"
  location                   = azurerm_resource_group.this.location
  resource_group_name        = azurerm_resource_group.this.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 90
  purge_protection_enabled   = true
  rbac_authorization_enabled = true
  tags                       = var.tags
}

data "azurerm_client_config" "current" {}

resource "azurerm_role_assignment" "terraform_key_vault_secrets_officer" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

ephemeral "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

resource "azurerm_key_vault_secret" "postgres_password" {
  name             = "pg-admin-password"
  value_wo         = ephemeral.random_password.postgres_admin.result
  value_wo_version = var.secret_version
  key_vault_id     = azurerm_key_vault.this.id
  depends_on       = [azurerm_role_assignment.terraform_key_vault_secrets_officer]
}

ephemeral "azurerm_key_vault_secret" "postgres_password" {
  name         = azurerm_key_vault_secret.postgres_password.name
  key_vault_id = azurerm_key_vault.this.id
}

resource "azurerm_key_vault_secret" "jwt_signing_key" {
  name             = "jwt-signing-key"
  value_wo         = ephemeral.random_password.jwt_signing_key.result
  value_wo_version = var.secret_version
  key_vault_id     = azurerm_key_vault.this.id
  depends_on       = [azurerm_role_assignment.terraform_key_vault_secrets_officer]
}

resource "azurerm_key_vault_secret" "database_connection_string" {
  name             = "database-connection-string"
  value_wo         = "Host=${azurerm_postgresql_flexible_server.this.fqdn};Database=${azurerm_postgresql_flexible_server_database.tradebook.name};Username=${var.postgres_admin_login};Password=${ephemeral.azurerm_key_vault_secret.postgres_password.value};Ssl Mode=Require"
  value_wo_version = var.secret_version
  key_vault_id     = azurerm_key_vault.this.id
  depends_on       = [azurerm_role_assignment.terraform_key_vault_secrets_officer]
}
