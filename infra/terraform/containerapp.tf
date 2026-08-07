resource "azurerm_container_app_environment" "this" {
  name                       = "cae-${var.name_prefix}-${var.environment}"
  location                   = azurerm_resource_group.this.location
  resource_group_name        = azurerm_resource_group.this.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
  infrastructure_subnet_id   = azurerm_subnet.container_apps.id
  tags                       = var.tags
}

resource "azurerm_container_app" "api" {
  name                         = "tradebook-api"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = azurerm_resource_group.this.name
  revision_mode                = "Single"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_app.id]
  }

  secret {
    name                = "database-connection-string"
    key_vault_secret_id = azurerm_key_vault_secret.database_connection_string.versionless_id
    identity            = azurerm_user_assigned_identity.container_app.id
  }

  secret {
    name                = "jwt-signing-key"
    key_vault_secret_id = azurerm_key_vault_secret.jwt_signing_key.versionless_id
    identity            = azurerm_user_assigned_identity.container_app.id
  }

  template {
    min_replicas = 1
    max_replicas = 2

    container {
      name   = "api"
      image  = var.api_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name        = "Database__ConnectionString"
        secret_name = "database-connection-string"
      }
      env {
        name  = "Jwt__Issuer"
        value = "Tradebook"
      }
      env {
        name  = "Jwt__Audience"
        value = "Tradebook"
      }
      env {
        name        = "Jwt__SigningKey"
        secret_name = "jwt-signing-key"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  depends_on = [azurerm_role_assignment.key_vault_secrets_user]
}
