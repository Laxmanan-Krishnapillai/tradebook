locals {
  workload_identities = toset(["api", "migration", "backup", "restore"])
}

resource "azurerm_user_assigned_identity" "workload" {
  for_each = local.workload_identities

  name                = "id-${var.name_prefix}-${var.environment}-${each.key}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  tags                = var.tags
}

resource "azurerm_role_assignment" "acr_pull" {
  for_each = local.workload_identities

  scope                = azurerm_container_registry.this.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.workload[each.key].principal_id
}

resource "azurerm_role_assignment" "key_vault_secrets_user" {
  for_each = local.workload_identities

  scope                = azurerm_key_vault.this.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.workload[each.key].principal_id
}

resource "azurerm_role_assignment" "backup_blob_contributor" {
  scope                = azurerm_storage_container.backups.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.workload["backup"].principal_id
}

resource "azurerm_role_assignment" "restore_blob_reader" {
  scope                = azurerm_storage_container.backups.id
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = azurerm_user_assigned_identity.workload["restore"].principal_id
}
