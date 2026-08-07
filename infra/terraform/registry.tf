resource "azurerm_container_registry" "this" {
  name                          = "cr${replace(var.name_prefix, "-", "")}${var.environment}"
  resource_group_name           = azurerm_resource_group.this.name
  location                      = azurerm_resource_group.this.location
  sku                           = "Basic"
  admin_enabled                 = false
  public_network_access_enabled = true
  tags                          = var.tags
}
