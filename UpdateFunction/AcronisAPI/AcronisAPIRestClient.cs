using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using RestSharp;
using RestSharp.Serializers.NewtonsoftJson;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using azuregeek.AZAcronisUpdater.AcronisAPI.Models;

namespace azuregeek.AZAcronisUpdater.AcronisAPI
{
    internal class AcronisAPIRestClient
    {
        private Uri _baseCloudUri;
        private Uri _baseTenantUri;        
        private RestClient _client;
        private ILogger _log;
        private int _apiTimeout;
        
        // User/Tenant info
        private APIUser _signedInUser;
        private APITenant _signedInTenant;

        // Update Tenants Bag
        private ConcurrentBag<APITenant> _updateTenants;
        private int _updateTenantsMaxDepth;

        // Auth info                
        private bool _authenticated = false;
        private string _authToken;
        private bool _scopedAuth = false;
        private string _scopedAuthToken;

        internal AcronisAPIRestClient(Uri baseCloudUri, ref ILogger log, int apiTimeout)
        {
            _baseCloudUri = baseCloudUri;
            _log = log;
            _apiTimeout = apiTimeout;            
        }
        internal AcronisAPIRestClient(ref ILogger log, int apiTimeout)
        {
            _baseCloudUri = new Uri("https://cloud.acronis.com");
            _log = log;
            _apiTimeout = apiTimeout;            
        }

        /// <summary>Sign into Acronis Cloud API</summary>
        /// <param name="username">Acronis Cloud Username. MFA has to be disabled.</param>
        /// <param name="password">Acronis Cloud Password.</param>        
        public async Task Authenticate(string username, string password)
        {
            // Get tenant URI
            _baseTenantUri = await GetAcronisTenantUri(username);

            // Instantiate rest client
            _client = new RestClient(_baseTenantUri)
            {
                Authenticator = new AcronisAPIAuthenticator(username, password, _baseTenantUri),
                Encoding = Encoding.UTF8,
                Timeout = ApiTimeout,

            };
            _client.UseNewtonsoftJson();

            // Instantiate propertys
            _updateTenants = new ConcurrentBag<APITenant>();
            _updateTenantsMaxDepth = 0;
            _signedInUser = await GetUser();
            _signedInTenant = await GetTenant(SignedInUser.TenantID);
            _signedInTenant.TenantUsers = await GetUsersForTenant(SignedInUser.TenantID);
            _authToken = ((IAcronisAuthenticator)_client.Authenticator).AuthToken;
            _authenticated = true;

            _log.LogDebug($"Signed in as login {SignedInUser.UserName} with e-mail {SignedInUser.Email} ");
            _log.LogDebug($"Sign in tenant: {SignedInTenant.Name} ({SignedInTenant.TenantKind})");
        }

        /// <summary>Sign into Acronis Cloud API</summary>
        /// <param name="topTenantApiClient">Tenant object that has been used to authenticate on top level.</param>        
        /// <param name="signInTenantId">Tenant ID to sign into (impersonation / scoped authentication)</param>
        public void AuthenticateScoped(AcronisAPIRestClient topTenantApiClient, Guid signIntoTenantId)
        {
            if (topTenantApiClient.Authenticated)
            {
                // Instantiate rest client
                _client = new RestClient(topTenantApiClient._baseTenantUri)
                {
                    Authenticator = new AcronisAPIAuthenticator(topTenantApiClient.AuthToken, signIntoTenantId, topTenantApiClient.TenantBaseUri),
                    Encoding = Encoding.UTF8,
                    Timeout = ApiTimeout,
                };
                _client.UseNewtonsoftJson();

                // Instantiate propertys                
                _updateTenantsMaxDepth = 0;

                _scopedAuthToken = ((IAcronisAuthenticator)_client.Authenticator).AuthToken;
                _authenticated = true;
                _scopedAuth = true;

                _log.LogDebug($"Scoped authentication into tenant ID {signIntoTenantId} succeeded");                
            }
            else
                throw new InvalidOperationException("Top Tenant API client has to be authenticated first");

        }

        public async Task<ConcurrentBag<APIAgent>> GetAgents(Guid tenantID)
        {
            var request = new RestRequest("bc/api/resource_manager/v1/agents", Method.GET);
            var response = await _client.ExecuteAsync(request);
            
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            var agentBag = new ConcurrentBag<APIAgent>();

            foreach (JToken item in responseJson.SelectToken("items"))
            {
                var apiAgentItem = new APIAgent();                
                apiAgentItem.AgentID = new Guid(item.Value<string>("id"));                
                apiAgentItem.AgentVersion = item.SelectToken("details").Value<string>("version") ?? string.Empty;
                apiAgentItem.AvailableUpdateVersion = item.SelectToken("updateState").Value<string>("version") ?? string.Empty;
                apiAgentItem.Hostname = item.Value<string>("name") ?? string.Empty;
                apiAgentItem.Online = item.SelectToken("communication").Value<bool>("online");
                apiAgentItem.OS = item.SelectToken("details").SelectToken("os").Value<string>("name") ?? string.Empty;
                apiAgentItem.TenantID = tenantID;
                apiAgentItem.UpdateAvailable = string.IsNullOrEmpty(item.SelectToken("updateState").Value<string>("status")) ? false : item.SelectToken("updateState").Value<string>("status").Equals("available");
                agentBag.Add(apiAgentItem);
            }

            return agentBag;
        }

        public async Task<Guid> UpdateAgent(Guid agentGuid)
        {
            JObject requestJson = new JObject(
                new JProperty("machinesIds",
                    new JArray(agentGuid.ToString())
            ));

            var request = new RestRequest("bc/api/ams/resource_operations/run_auto_update", Method.POST);            
            request.AddJsonBody(requestJson);            
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            string activitityID = (string)responseJson.SelectToken("data[0].activity_id");

            return new Guid(activitityID);
        }

        public async Task<APIUser> GetUser()
        {
            // Get user Info
            var request = new RestRequest($"api/2/users/me", Method.GET);
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            var user = new APIUser();
            user.UserName = responseJson.Value<string>("login") ?? string.Empty;
            user.Firstname = responseJson.SelectToken("contact").Value<string>("firstname") ?? string.Empty;
            user.Lastname = responseJson.SelectToken("contact").Value<string>("lastname") ?? string.Empty;
            user.Email = responseJson.SelectToken("contact").Value<string>("email") ?? string.Empty;
            user.UserID = new Guid(responseJson.Value<string>("id"));
            user.PersonalTenantID = string.IsNullOrEmpty(responseJson.Value<string>("personal_tenant_id")) ? new Guid(): new Guid(responseJson.Value<string>("personal_tenant_id"));
            user.TenantID = new Guid(responseJson.Value<string>("tenant_id"));

            return user;
        }
        public async Task<APIUser> GetUser(Guid UserID)
        {
            // Get user Info
            var request = new RestRequest($"api/2/users/{UserID}", Method.GET);
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            var user = new APIUser();
            user.UserName = responseJson.Value<string>("login") ?? string.Empty;
            user.Firstname = responseJson.SelectToken("contact").Value<string>("firstname") ?? string.Empty;
            user.Lastname = responseJson.SelectToken("contact").Value<string>("lastname") ?? string.Empty;
            user.Email = responseJson.SelectToken("contact").Value<string>("email") ?? string.Empty;
            user.UserID = new Guid(responseJson.Value<string>("id"));
            user.PersonalTenantID = string.IsNullOrEmpty(responseJson.Value<string>("personal_tenant_id")) ? new Guid() : new Guid(responseJson.Value<string>("personal_tenant_id"));
            user.TenantID = new Guid(responseJson.Value<string>("tenant_id"));

            return user;
        }

        public async Task<APITenant> GetTenant(Guid tenantGuid)
        {            
            var request = new RestRequest($"api/2/tenants/{tenantGuid}", Method.GET);
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            var tenant = new APITenant();

            tenant.TenantID = new Guid(responseJson.Value<string>("id"));
            tenant.Name = responseJson.Value<string>("name") ?? string.Empty;
            tenant.TenantKind = responseJson.Value<string>("kind") ?? string.Empty;
            tenant.ParentTenantID = new Guid(responseJson.Value<string>("parent_id"));
            tenant.ParentTenantName = string.Empty;
            
            return tenant;
        }

        public async Task<ConcurrentBag<APIUser>> GetUsersForTenant(Guid tenantGuid)
        {
            var request = new RestRequest($"api/2/tenants/{tenantGuid}/users", Method.GET);
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            JArray userIdArray = (JArray)responseJson.SelectToken("items");

            var usersBag = new ConcurrentBag<APIUser>();

            foreach (var item in userIdArray)
            {
                Guid userID = new Guid((string)item);
                var user = await GetUser(userID);
                usersBag.Add(user);
            }

            return usersBag;
        }

        public async Task<ConcurrentBag<APITenant>> GetAllTenantsForUpdate()
        {
            // Process:
            // 1. Build bag with tenant of current user (only 1 item)
            // 2. Call GetSubTenants to Iterate first level of Sub Tenants and add Client Tenants to _updateTenants bag
            // 3. GetSubTenants calls itself to add clients of additional sub-level to _updateTenants bag, until no more levels are available

            _log.LogInformation($"Start - Collecting client Tenants");
            DateTime startDateTime = DateTime.Now;

            var topTenantBag = new ConcurrentBag<APITenant>();
            topTenantBag.Add(this.SignedInTenant);
            await this.GetClientInSubTenants(topTenantBag, 0);

            TimeSpan collectionDuration = (DateTime.Now).Subtract(startDateTime);

            _log.LogInformation($"Finished - Collected {UpdateTenants.Count} client tenants. Maximum depth: {UpdateTenantsMaxDepth}. Collection took {collectionDuration.TotalSeconds.ToString("F2")} Seconds.");

            return UpdateTenants;
        }
                
        // Propertys
        public Uri TenantBaseUri
        {
            get => _baseTenantUri;
        }
        public APIUser SignedInUser
        {
            get => _signedInUser;
        }
        public APITenant SignedInTenant
        {
            get => _signedInTenant;
        }
        public ConcurrentBag<APITenant> UpdateTenants
        {
            get => _updateTenants;
        }
        public Int32 UpdateTenantsMaxDepth
        {
            get => _updateTenantsMaxDepth;
        }
        public List<Guid> ExcludeTenantIdsList { get; set; }
        public bool ScopedAuth
        {
            get => _scopedAuth;
        }
        public bool Authenticated
        {
            get => _authenticated;
        }
        public string AuthToken
        {
            get => _authToken;
        }
        public Int32 ApiTimeout
        {
            get { return _apiTimeout > 0 ? _apiTimeout : 6000; }
            set { _apiTimeout = value; }
        }

        // private methods
        private async Task<Uri> GetAcronisTenantUri(string username)
        {
            var client = new RestClient(_baseCloudUri);
            var request = new RestRequest("api/1/accounts/", Method.GET);
            request.AddParameter("login", username);

            var response = await client.ExecuteAsync(request);
            JObject responseJson = JObject.Parse(response.Content);
            string authUriString = (string)responseJson.SelectToken("server_url");

            if (string.IsNullOrEmpty(authUriString))
            {
                throw new InvalidOperationException("The authentication URL (Tenant Login URL) received by the server is null or empty");
            }

            return (new Uri(authUriString));
        }

        private async Task<ConcurrentBag<APITenant>> GetSubTenants(APITenant topTenant)
        {
            _log.LogDebug($"Getting sub tenants for tenant {topTenant.Name} ({topTenant.TenantKind})");
            var request = new RestRequest($"api/2/tenants", Method.GET);
            request.AddParameter("parent_id", topTenant.TenantID, ParameterType.GetOrPost);
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            var apiTenantBag = new ConcurrentBag<APITenant>();

            foreach(JToken item in responseJson.SelectToken("items"))
            {
                var apiTenantItem = new APITenant();
                apiTenantItem.TenantID = new Guid(item.Value<string>("id"));
                apiTenantItem.Name = item.Value<string>("name") ?? string.Empty;
                apiTenantItem.TenantKind = item.Value<string>("kind") ?? string.Empty;                
                apiTenantItem.ParentTenantID = new Guid(item.Value<string>("parent_id"));
                apiTenantItem.ParentTenantName = topTenant.Name;
                apiTenantItem.TenantUsers = await GetUsersForTenant(apiTenantItem.TenantID);
                apiTenantBag.Add(apiTenantItem);
            }

            return apiTenantBag;
        }

        private async Task GetClientInSubTenants (ConcurrentBag<APITenant> topTenants, Int32 level)
        {            
            level += 1;
            if (level > _updateTenantsMaxDepth)
                _updateTenantsMaxDepth = level;

            foreach(APITenant apiTenantItem in topTenants)
            {
                // check if tenant is excluded
                if(ExcludeTenantIdsList.Contains(apiTenantItem.TenantID))
                {
                    _log.LogInformation($"Tenant {apiTenantItem.Name} ({apiTenantItem.TenantKind}) at level {level} is excluded and will be skipped");
                    continue;
                }

                _log.LogDebug($"Processing sub tenants for tenant {apiTenantItem.Name} ({apiTenantItem.TenantKind}) at level {level}");
                                
                // Tenants of kind customer and unit can contain agents to update
                if (apiTenantItem.TenantKind == "customer" || apiTenantItem.TenantKind == "unit")
                    _updateTenants.Add(apiTenantItem);

                // Tenants of kind partner, folder and unit can contain another level of tenants
                // http://dl.managed-protection.com/u/baas/help/8.0/partner/en-US/index.html#40201.html
                if (apiTenantItem.TenantKind == "partner" || apiTenantItem.TenantKind == "folder" || apiTenantItem.TenantKind == "unit")
                {
                    ConcurrentBag<APITenant> subTenantList = await this.GetSubTenants(apiTenantItem);
                    await GetClientInSubTenants(subTenantList, level);                    
                }

            }
        }
    }
}
