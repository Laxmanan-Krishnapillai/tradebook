resource "azurerm_log_analytics_workspace" "this" {
  name                = "log-${var.name_prefix}-${var.environment}"
  location            = azurerm_resource_group.this.location
  resource_group_name = azurerm_resource_group.this.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

resource "azurerm_monitor_diagnostic_setting" "postgres" {
  name                       = "postgres-to-log-analytics"
  target_resource_id         = azurerm_postgresql_flexible_server.this.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id

  enabled_log {
    category = "PostgreSQLLogs"
  }
  enabled_log {
    category = "PostgreSQLFlexSessions"
  }
  enabled_log {
    category = "PostgreSQLFlexDatabaseXacts"
  }
  enabled_metric {
    category = "AllMetrics"
  }
}

resource "azurerm_monitor_diagnostic_setting" "key_vault" {
  name                       = "key-vault-to-log-analytics"
  target_resource_id         = azurerm_key_vault.this.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id

  enabled_log {
    category = "AuditEvent"
  }
  enabled_metric {
    category = "AllMetrics"
  }
}

resource "azurerm_monitor_diagnostic_setting" "backup_blob" {
  name                       = "backup-blob-to-log-analytics"
  target_resource_id         = "${azurerm_storage_account.backups.id}/blobServices/default"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id

  enabled_log {
    category = "StorageRead"
  }
  enabled_log {
    category = "StorageWrite"
  }
  enabled_log {
    category = "StorageDelete"
  }
  enabled_metric {
    category = "Transaction"
  }
}

resource "azurerm_monitor_action_group" "operations" {
  name                = "ag-${var.name_prefix}-${var.environment}-operations"
  resource_group_name = azurerm_resource_group.this.name
  short_name          = "tradebookops"
  tags                = var.tags

  dynamic "email_receiver" {
    for_each = var.alert_email == null ? [] : [var.alert_email]
    content {
      name          = "operations"
      email_address = email_receiver.value
    }
  }
}

resource "azurerm_monitor_metric_alert" "api_no_replicas" {
  name                = "tradebook-api-no-replicas"
  resource_group_name = azurerm_resource_group.this.name
  scopes              = [azurerm_container_app.api.id]
  description         = "Tradebook API has no ready replicas."
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.App/containerapps"
    metric_name      = "Replicas"
    aggregation      = "Average"
    operator         = "LessThan"
    threshold        = 1
  }

  action {
    action_group_id = azurerm_monitor_action_group.operations.id
  }
}

resource "azurerm_monitor_metric_alert" "api_server_errors" {
  name                = "tradebook-api-server-errors"
  resource_group_name = azurerm_resource_group.this.name
  scopes              = [azurerm_container_app.api.id]
  description         = "Tradebook API returned more than five 5xx responses in five minutes."
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.App/containerapps"
    metric_name      = "Requests"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 5

    dimension {
      name     = "statusCodeCategory"
      operator = "Include"
      values   = ["5xx"]
    }
  }

  action {
    action_group_id = azurerm_monitor_action_group.operations.id
  }
}

resource "azurerm_monitor_metric_alert" "postgres_unavailable" {
  name                = "tradebook-postgres-unavailable"
  resource_group_name = azurerm_resource_group.this.name
  scopes              = [azurerm_postgresql_flexible_server.this.id]
  description         = "PostgreSQL Flexible Server availability metric is unhealthy."
  severity            = 0
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "is_db_alive"
    aggregation      = "Maximum"
    operator         = "LessThan"
    threshold        = 1
  }

  action {
    action_group_id = azurerm_monitor_action_group.operations.id
  }
}

resource "azurerm_monitor_metric_alert" "postgres_storage" {
  name                = "tradebook-postgres-storage"
  resource_group_name = azurerm_resource_group.this.name
  scopes              = [azurerm_postgresql_flexible_server.this.id]
  description         = "PostgreSQL Flexible Server storage exceeds 80 percent."
  severity            = 1
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "storage_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  action {
    action_group_id = azurerm_monitor_action_group.operations.id
  }
}

resource "azurerm_monitor_metric_alert" "postgres_cpu" {
  name                = "tradebook-postgres-cpu"
  resource_group_name = azurerm_resource_group.this.name
  scopes              = [azurerm_postgresql_flexible_server.this.id]
  description         = "PostgreSQL Flexible Server CPU exceeds 85 percent."
  severity            = 2
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "cpu_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 85
  }

  action {
    action_group_id = azurerm_monitor_action_group.operations.id
  }
}

locals {
  monitored_jobs = {
    migration = azurerm_container_app_job.migration.id
    backup    = azurerm_container_app_job.backup.id
    restore   = azurerm_container_app_job.restore.id
  }
}

resource "azurerm_monitor_metric_alert" "job_failed" {
  for_each = local.monitored_jobs

  name                = "tradebook-${each.key}-job-failed"
  resource_group_name = azurerm_resource_group.this.name
  scopes              = [each.value]
  description         = "Tradebook ${each.key} Container Apps Job failed."
  severity            = 1
  frequency           = "PT1M"
  window_size         = "PT5M"

  criteria {
    metric_namespace = "Microsoft.App/jobs"
    metric_name      = "Executions"
    aggregation      = "Total"
    operator         = "GreaterThan"
    threshold        = 0

    dimension {
      name     = "state"
      operator = "Include"
      values   = ["Failed"]
    }
  }

  action {
    action_group_id = azurerm_monitor_action_group.operations.id
  }
}

resource "azurerm_monitor_scheduled_query_rules_alert_v2" "backup_missing" {
  name                  = "tradebook-backup-missing"
  resource_group_name   = azurerm_resource_group.this.name
  location              = azurerm_resource_group.this.location
  scopes                = [azurerm_log_analytics_workspace.this.id]
  description           = "No successful Tradebook pg_dump completion was logged during the last day."
  severity              = 1
  enabled               = true
  skip_query_validation = true
  evaluation_frequency  = "PT1H"
  window_duration       = "P1D"

  criteria {
    query                   = "ContainerAppConsoleLogs_CL | where Log_s has '\"event\":\"backup.completed\"'"
    time_aggregation_method = "Count"
    threshold               = 1
    operator                = "LessThan"

    failing_periods {
      minimum_failing_periods_to_trigger_alert = 1
      number_of_evaluation_periods             = 1
    }
  }

  action {
    action_groups = [azurerm_monitor_action_group.operations.id]
  }
}
