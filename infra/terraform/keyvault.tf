resource "azurerm_user_assigned_identity" "container_app" {
  name                = "id-${var.name_prefix}-${var.environment}-api"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  tags                = var.tags
}

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

resource "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

resource "azurerm_key_vault_secret" "postgres_password" {
  name         = "pg-admin-password"
  value        = random_password.postgres_admin.result
  key_vault_id = azurerm_key_vault.this.id
}

resource "azurerm_key_vault_secret" "jwt_signing_key" {
  name         = "jwt-signing-key"
  value        = random_password.jwt_signing_key.result
  key_vault_id = azurerm_key_vault.this.id
}

resource "azurerm_key_vault_secret" "database_connection_string" {
  name         = "database-connection-string"
  value        = "Host=${azurerm_postgresql_flexible_server.this.fqdn};Database=${azurerm_postgresql_flexible_server_database.tradebook.name};Username=${var.postgres_admin_login};Password=${random_password.postgres_admin.result};Ssl Mode=Require"
  key_vault_id = azurerm_key_vault.this.id
}

resource "azurerm_role_assignment" "key_vault_secrets_user" {
  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.container_app.principal_id
}
