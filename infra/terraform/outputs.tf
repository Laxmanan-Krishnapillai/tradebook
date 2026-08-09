output "api_fqdn" {
  value = azurerm_container_app.api.ingress[0].fqdn
}

output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.this.fqdn
}

output "backup_storage_name" {
  value = azurerm_storage_account.backups.name
}

output "container_registry_login_server" {
  value = azurerm_container_registry.this.login_server
}

output "migration_job_name" {
  value = azurerm_container_app_job.migration.name
}

output "backup_job_name" {
  value = azurerm_container_app_job.backup.name
}

output "entra_tenant_id" { value = var.entra_tenant_id }
output "entra_spa_client_id" { value = azuread_application.spa.client_id }
output "entra_api_client_id" { value = azuread_application.api.client_id }
output "entra_api_scope" { value = "api://${azuread_application.api.client_id}/access_as_user" }
