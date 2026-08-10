locals {
  tradebook_scope_id = "8dd6c2e1-c39b-4c7f-9f36-4fe7cdbeb345"
  tradebook_roles = {
    Trader     = "e10adead-3505-4cc0-8ce5-9c0ea8866221"
    BackOffice = "cf9a1529-936f-4ccd-b671-56acbc570931"
    Admin      = "bf43e477-f117-4e55-a24e-59382b6984f8"
  }
}

resource "azuread_application" "api" {
  display_name     = "Tradebook API (${var.environment})"
  sign_in_audience = "AzureADMyOrg"
  api {
    requested_access_token_version = 2
    oauth2_permission_scope {
      admin_consent_description  = "Allow Tradebook users to access the Tradebook API."
      admin_consent_display_name = "Access Tradebook as a user"
      enabled                    = true
      id                         = local.tradebook_scope_id
      type                       = "Admin"
      user_consent_description   = "Access Tradebook as you."
      user_consent_display_name  = "Access Tradebook"
      value                      = "access_as_user"
    }
  }
  dynamic "app_role" {
    for_each = local.tradebook_roles
    content {
      allowed_member_types = ["User"]
      description          = "Tradebook ${app_role.key} access"
      display_name         = app_role.key
      enabled              = true
      id                   = app_role.value
      value                = app_role.key
    }
  }
}

resource "azuread_application_identifier_uri" "api" {
  application_id = azuread_application.api.id
  identifier_uri = "api://${azuread_application.api.client_id}"
}

resource "azuread_service_principal" "api" {
  client_id                    = azuread_application.api.client_id
  app_role_assignment_required = true
}

resource "azuread_application" "spa" {
  display_name     = "Tradebook SPA (${var.environment})"
  sign_in_audience = "AzureADMyOrg"
  single_page_application {
    redirect_uris = var.entra_redirect_uris
  }
  required_resource_access {
    resource_app_id = azuread_application.api.client_id
    resource_access {
      id   = local.tradebook_scope_id
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "spa" { client_id = azuread_application.spa.client_id }

resource "azuread_service_principal_delegated_permission_grant" "spa_api" {
  service_principal_object_id          = azuread_service_principal.spa.object_id
  resource_service_principal_object_id = azuread_service_principal.api.object_id
  claim_values                         = ["access_as_user"]
}
