locals {
  database_job_environment = {
    PGHOST     = azurerm_postgresql_flexible_server.this.fqdn
    PGPORT     = "5432"
    PGDATABASE = azurerm_postgresql_flexible_server_database.tradebook.name
    PGUSER     = var.postgres_admin_login
    PGSSLMODE  = "require"
  }
}

resource "azurerm_container_app_job" "migration" {
  name                         = "tradebook-migration"
  location                     = azurerm_resource_group.this.location
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = azurerm_container_app_environment.this.id
  replica_timeout_in_seconds   = 1800
  replica_retry_limit          = 0
  workload_profile_name        = "Consumption"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.workload["migration"].id]
  }

  registry {
    server   = azurerm_container_registry.this.login_server
    identity = azurerm_user_assigned_identity.workload["migration"].id
  }

  secret {
    name                = "pg-password"
    key_vault_secret_id = azurerm_key_vault_secret.postgres_password.versionless_id
    identity            = azurerm_user_assigned_identity.workload["migration"].id
  }

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name    = "migration"
      image   = var.database_ops_image
      cpu     = 0.5
      memory  = "1Gi"
      command = ["/bin/bash"]
      args    = ["/opt/tradebook/database-ops/run-migrations.sh"]

      dynamic "env" {
        for_each = local.database_job_environment
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name        = "PGPASSWORD"
        secret_name = "pg-password"
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.key_vault_secrets_user
  ]
}

resource "azurerm_container_app_job" "backup" {
  name                         = "tradebook-backup"
  location                     = azurerm_resource_group.this.location
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = azurerm_container_app_environment.this.id
  replica_timeout_in_seconds   = 3600
  replica_retry_limit          = 2
  workload_profile_name        = "Consumption"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.workload["backup"].id]
  }

  registry {
    server   = azurerm_container_registry.this.login_server
    identity = azurerm_user_assigned_identity.workload["backup"].id
  }

  secret {
    name                = "pg-password"
    key_vault_secret_id = azurerm_key_vault_secret.postgres_password.versionless_id
    identity            = azurerm_user_assigned_identity.workload["backup"].id
  }

  schedule_trigger_config {
    cron_expression          = "0 2 * * *"
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name    = "backup"
      image   = var.database_ops_image
      cpu     = 0.5
      memory  = "1Gi"
      command = ["/bin/bash"]
      args    = ["/opt/tradebook/database-ops/backup.sh"]

      dynamic "env" {
        for_each = local.database_job_environment
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name        = "PGPASSWORD"
        secret_name = "pg-password"
      }
      env {
        name  = "AZURE_STORAGE_ACCOUNT"
        value = azurerm_storage_account.backups.name
      }
      env {
        name  = "AZURE_STORAGE_CONTAINER"
        value = azurerm_storage_container.backups.name
      }
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.workload["backup"].client_id
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.key_vault_secrets_user,
    azurerm_role_assignment.backup_blob_contributor
  ]
}

resource "azurerm_container_app_job" "restore" {
  name                         = "tradebook-restore"
  location                     = azurerm_resource_group.this.location
  resource_group_name          = azurerm_resource_group.this.name
  container_app_environment_id = azurerm_container_app_environment.this.id
  replica_timeout_in_seconds   = 3600
  replica_retry_limit          = 0
  workload_profile_name        = "Consumption"
  tags                         = var.tags

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.workload["restore"].id]
  }

  registry {
    server   = azurerm_container_registry.this.login_server
    identity = azurerm_user_assigned_identity.workload["restore"].id
  }

  secret {
    name                = "pg-password"
    key_vault_secret_id = azurerm_key_vault_secret.postgres_password.versionless_id
    identity            = azurerm_user_assigned_identity.workload["restore"].id
  }

  manual_trigger_config {
    parallelism              = 1
    replica_completion_count = 1
  }

  template {
    container {
      name    = "restore"
      image   = var.database_ops_image
      cpu     = 0.5
      memory  = "1Gi"
      command = ["/bin/bash"]
      args    = ["/opt/tradebook/database-ops/restore.sh"]

      dynamic "env" {
        for_each = merge(local.database_job_environment, { PGDATABASE = "postgres" })
        content {
          name  = env.key
          value = env.value
        }
      }

      env {
        name        = "PGPASSWORD"
        secret_name = "pg-password"
      }
      env {
        name  = "AZURE_STORAGE_ACCOUNT"
        value = azurerm_storage_account.backups.name
      }
      env {
        name  = "AZURE_STORAGE_CONTAINER"
        value = azurerm_storage_container.backups.name
      }
      env {
        name  = "AZURE_CLIENT_ID"
        value = azurerm_user_assigned_identity.workload["restore"].client_id
      }
      env {
        name  = "BACKUP_BLOB"
        value = "tradebook/1970/01/not-configured.dump"
      }
      env {
        name  = "RESTORE_DATABASE"
        value = "tradebook_restore_validation"
      }
    }
  }

  lifecycle {
    ignore_changes = [template[0].container[0].image]
  }

  depends_on = [
    azurerm_role_assignment.acr_pull,
    azurerm_role_assignment.key_vault_secrets_user,
    azurerm_role_assignment.restore_blob_reader
  ]
}
