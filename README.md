# AcronisAgentUpdater
Azure Function to automatically update Acronis Backup Cloud Agents with the following features:
- Fully automated Deployment
- Login via Username / Password (protected by Azure KeyVault) into Acronis API
- Identify all agents in signed in tenant
- Iterate every tenant of every kind (Partner, Customer, Unit, Folder) and identify agents in those sub tenants
- Update all identified tenants
- Write updated tenants into Azure Table Storage

## Prerequisites
- Acronis Backup Cloud Admin User with full permissions
- Acronis User has to be defined as "Service Account" without MFA enabled
