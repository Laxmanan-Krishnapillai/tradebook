output "api_fqdn" {
  value = azurerm_container_app.api.ingress[0].fqdn
}

output "postgres_fqdn" {
  value = azurerm_postgresql_flexible_server.this.fqdn
}

output "backup_storage_name" {
  value = azurerm_storage_account.backups.name
}
