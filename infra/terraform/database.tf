ephemeral "random_password" "postgres_admin" {
  length           = 40
  special          = true
  override_special = "_%@-"
}

resource "azurerm_postgresql_flexible_server" "this" {
  name                              = "psql-${var.name_prefix}-${var.environment}"
  resource_group_name               = azurerm_resource_group.this.name
  location                          = azurerm_resource_group.this.location
  version                           = "17"
  administrator_login               = var.postgres_admin_login
  administrator_password_wo         = ephemeral.azurerm_key_vault_secret.postgres_password.value
  administrator_password_wo_version = var.secret_version
  sku_name                          = "B_Standard_B2s"
  storage_mb                        = 32768
  backup_retention_days             = 7
  delegated_subnet_id               = azurerm_subnet.postgres.id
  private_dns_zone_id               = azurerm_private_dns_zone.postgres.id
  public_network_access_enabled     = false
  tags                              = var.tags

  depends_on = [azurerm_private_dns_zone_virtual_network_link.postgres]
}

resource "azurerm_postgresql_flexible_server_database" "tradebook" {
  name      = "tradebook"
  server_id = azurerm_postgresql_flexible_server.this.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_configuration" "pg_stat_statements" {
  name      = "shared_preload_libraries"
  server_id = azurerm_postgresql_flexible_server.this.id
  value     = "pg_stat_statements"
}
