terraform {
  required_providers {
    azurerm = {
      source = "hashicorp/azurerm"
      version = ">= 2.42.0"
    }
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "rg" {
  name     = "rg-${var.prefix}-function"
  location = var.location

  tags = {
        environment = var.prefix
    }
}

resource "azurerm_storage_account" "storage" {
  name                     = "${var.prefix}storage"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  tags = {
        environment = var.prefix
    }
}

resource "azurerm_storage_container" "container" {
  name                  = "function-releases"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}

# BLOB containing the compiled and zipped function app
resource "azurerm_storage_blob" "appcode" {
    name = "functionapp.zip"
    storage_account_name = azurerm_storage_account.storage.name
    storage_container_name = azurerm_storage_container.container.name
    type = "Block"
    source = var.functionapp
}

# Shared Access Token allowing the function app to access the BLOB
data "azurerm_storage_account_sas" "sas" {
    connection_string = azurerm_storage_account.storage.primary_connection_string
    https_only = true
    start = "2021-01-01"
    expiry = "2023-12-31"
    resource_types {
        object = true
        container = false
        service = false
    }
    services {
        blob = true
        queue = false
        table = false
        file = false
    }
    permissions {
        read = true
        write = false
        delete = false
        list = false
        add = false
        create = false
        update = false
        process = false
    }
}

resource "azurerm_application_insights" "appinsights" {
  name                = "${var.prefix}-appinsights"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"

  # https://github.com/terraform-providers/terraform-provider-azurerm/issues/1303
  tags = {
    "hidden-link:${azurerm_resource_group.rg.id}/providers/Microsoft.Web/sites/${var.prefix}func" = "Resource"
    environment = var.prefix
  }

}

resource "azurerm_app_service_plan" "asp" {
  name                = "${var.prefix}-functions-consumption-asp"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  kind                = "FunctionApp"
  reserved            = true

  sku {
    tier = "Dynamic"
    size = "Y1"
  }

  tags = {
        environment = var.prefix
    }
}

resource "azurerm_function_app" "functionapp" {
  name                       = "${var.prefix}func"
  location                   = azurerm_resource_group.rg.location
  resource_group_name        = azurerm_resource_group.rg.name
  app_service_plan_id        = azurerm_app_service_plan.asp.id
  storage_account_name       = azurerm_storage_account.storage.name
  storage_account_access_key = azurerm_storage_account.storage.primary_access_key
  https_only                 = true
  os_type                    = "linux"
  version                    = "~3"

  app_settings = {
      HASH = base64encode(filesha256(var.functionapp))
      WEBSITE_RUN_FROM_PACKAGE = "https://${azurerm_storage_account.storage.name}.blob.core.windows.net/${azurerm_storage_container.container.name}/${azurerm_storage_blob.appcode.name}${data.azurerm_storage_account_sas.sas.sas}"
      "FUNCTIONS_WORKER_RUNTIME" = "dotnet"
      "FUNCTION_APP_EDIT_MODE" = "readonly"
      "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.appinsights.instrumentation_key
      "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.appinsights.connection_string
  }

  identity {
    type = "SystemAssigned"
  }

  tags = {
      environment = var.prefix
  }
}