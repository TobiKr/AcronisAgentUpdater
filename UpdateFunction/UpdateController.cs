using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using azuregeek.AZAcronisUpdater.AcronisAPI;
using azuregeek.AZAcronisUpdater.AcronisAPI.Models;
using azuregeek.AZAcronisUpdater.TableStorage;
using azuregeek.AZAcronisUpdater.TableStorage.Models;

namespace azuregeek.AZAcronisUpdater
{
    public static class UpdateController
    {
        [FunctionName("UpdateAllTenantAgents")]
        public static async Task<IActionResult> UpdateAllTenantAgents(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            // instantiate environment variables
            string acronisUsername = GetEnvironmentVariable("AcronisUsername");
            string acronisPassword = GetEnvironmentVariable("AcronisPassword");
            string acronisCloudBaseURL = GetEnvironmentVariable("AcronisCloudBaseURL");
            string acronisExcludeTenantIds = GetEnvironmentVariable("ExcludeTenantIds", true);
            Int32 apiTimeout = Convert.ToInt32(GetEnvironmentVariable("ApiTimeOut"));
            bool testMode = Convert.ToBoolean(GetEnvironmentVariable("TestMode"));
            string tableStorageConnectionString = GetEnvironmentVariable("AzureWebJobsStorage");

            // define variables
            List<Guid> excludeTenantList = new List<Guid>();
            string updateRunDateTime = DateTime.Now.ToString("s");

            log.LogInformation("C# HTTP trigger function is processing a request.");
            log.LogDebug($"Using Acronis Username {acronisUsername}");
            log.LogDebug($"Using Acronis Base URL {acronisCloudBaseURL}");
            log.LogDebug($"Update Run Timestamp: {updateRunDateTime}");

            // Get array of excluded tenants
            if(!string.IsNullOrEmpty(acronisExcludeTenantIds))
            {                
                string[] excludeListStr = acronisExcludeTenantIds.Split(",");
                foreach(string excludeStr in excludeListStr)
                {
                    try
                    {
                        Guid excludeGuid = new Guid(excludeStr);
                        excludeTenantList.Add(excludeGuid);
                        log.LogInformation($"Tenant Exclusion: added tenant ID {excludeStr} to exclusion list");
                    }
                    catch
                    {
                        log.LogError($"Tenant Exclusion: failed to parse tenant ID {excludeStr}");
                    }
                }
            }
            else
                log.LogInformation("Tenant Exclusion: no tenants identified");

            // Instantiate acronis API
            Uri acronisBaseUri = new Uri(acronisCloudBaseURL);
            AcronisAPI.AcronisAPIRestClient apiClient = new AcronisAPI.AcronisAPIRestClient(ref log, apiTimeout);
            apiClient.ExcludeTenantIdsList = excludeTenantList;

            // Authenticate and get auth token
            await apiClient.Authenticate(acronisUsername, acronisPassword);            
            log.LogDebug("Using Acronis Tenant URL " + apiClient.TenantBaseUri.ToString());
            log.LogInformation("Successfully authenticated");

            // Initiate Table Storage Client
            TableStorageClient tableStorageClient = new TableStorageClient(tableStorageConnectionString);
            log.LogInformation("Successfully initiated Table Storage");

            // Fetching all tenants and iterate them
            ConcurrentBag<APITenant> tenantBag = await apiClient.GetAllTenantsForUpdate();
            foreach(APITenant tenant in tenantBag)
            {
                log.LogInformation($"Processing tenant ID {tenant.TenantID} / {tenant.Name}");

                foreach (APIUser tenantUser in tenant.TenantUsers)
                {
                    log.LogDebug($"Processing tenant user {tenantUser.UserName}");

                    try
                    {
                        // Scoped auth to gain access to tenant resources
                        AcronisAPIRestClient scopedApiClient = new AcronisAPIRestClient(ref log, apiTimeout);
                        scopedApiClient.AuthenticateScoped(apiClient, tenantUser.PersonalTenantID);

                        // Getting all agents for scoped tenant
                        ConcurrentBag<APIAgent> agentBag = await scopedApiClient.GetAgents(tenant.TenantID);
                        foreach (APIAgent agent in agentBag)
                        {
                            if (agent.Online)
                            {
                                // !!!!!!!!!!!!!!!!!!!!!!
                                // CHANGE FOR PRODUCTION
                                // !!!!!!!!!!!!!!!!!!!!!!
                                if (!agent.UpdateAvailable)
                                {
                                    log.LogInformation($"Updating Agent {agent.Hostname} from version {agent.AgentVersion} to version {agent.AvailableUpdateVersion}");

                                    if (testMode)
                                    {
                                        log.LogWarning("Test Mode enabled, skipping update.");
                                        continue;
                                    }
                                    Guid updateActivtiyId = await scopedApiClient.UpdateAgent(agent.AgentID);
                                    log.LogDebug($"Update started - activity ID: {updateActivtiyId}");

                                    var updateEntity = new AgentUpdateEntity(updateRunDateTime, agent.AgentID);
                                    updateEntity.AgentOS = agent.OS;
                                    updateEntity.AgentVersionAfterUpdate = agent.AvailableUpdateVersion;
                                    updateEntity.AgentVersionBeforeUpdate = agent.AgentVersion;
                                    updateEntity.HostName = agent.Hostname;
                                    updateEntity.TenantID = tenant.TenantID.ToString();
                                    updateEntity.TenantName = tenant.Name;
                                    updateEntity.ParentTenantID = tenant.ParentTenantID.ToString();
                                    updateEntity.ParentTenantName = tenant.ParentTenantName;
                                    updateEntity.UpdateActivityID = updateActivtiyId.ToString();

                                    var tableResult = await tableStorageClient.InsertAgentUpdateEntity(updateEntity);
                                    log.LogDebug($"Written Agent Update Entity to Table Storage. Result: {tableResult.HttpStatusCode} / {tableResult.Etag}");
                                }
                                else
                                    log.LogInformation($"Agent {agent.Hostname} version {agent.AvailableUpdateVersion} is already up-to-date");
                            }
                            else
                            {
                                log.LogWarning($"Agent {agent.Hostname} is offline and will not being updated!");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogError($"Error processing tenant: {ex.Message}");
                    }
                }
                log.LogInformation($"Finished processing tenant ID {tenant.TenantID} / {tenant.Name}");
            }

            return new OkObjectResult("Processed ");
        }

        // Helper Functions
        private static string GetEnvironmentVariable(string name, bool nullable = false )
        {
            if(!nullable)
            {
                if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)))
                    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
                else
                    throw new InvalidOperationException($"Environment Variable {name} is null or empty");
            }
            else
               return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }
}