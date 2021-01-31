# AcronisAgentUpdater
Azure Function to automatically update Acronis Backup Cloud Agents periodically with the following features:
- Fully automated Deployment
- Update schedule set via cron expression
- Protect Credentials / Azure resources access tokens with Azure KeyVault as token based sign in is not supported by Acronis API
- Exclude specific tenants
- Identify all agents in signed in tenant
- Iterate every tenant of every kind (Partner, Customer, Unit, Folder) and identify agents in those sub tenants
- Update all identified tenants
- Write updated tenants into Azure Table Storage
- Send email notification after successful update

## Prerequisites
- Acronis Backup Cloud Admin User with full permissions
- Acronis User has to be defined as "Service Account" without MFA enabled
- Azure Subscription with the permission to create resources

## Deployment
Please use the "Deploy to Azure" Button below to deploy the function. If you have to reconfigure settings, please use the Azure Function Settings (https://docs.microsoft.com/azure/azure-functions/functions-how-to-use-azure-function-app-settings?tabs=portal#work-with-application-settings).

## Resources
The following resources are deployed:

| Resource             | Description                                                                    |
|----------------------|--------------------------------------------------------------------------------|
| Function App         | Hosting and managing the function code                                         |
| App Service Plan     | Consumption plan to run the function code                                      |
| Storage Account      | Internal Store for function app and Table Storage to store updated Agents      |
| Key Vault            | Stores Storage Access Token, Username/Password for Acronis API and Mail Server |
| Application Insight  | Monitors the Function App                                                      |


[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FTobiKr%2FAcronisAgentUpdater%2Fmain%2Fazuredeploy.json)

