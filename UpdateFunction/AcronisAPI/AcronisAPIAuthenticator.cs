using System;
using System.IO;
using System.Net;
using System.Security;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;

namespace azuregeek.AZAcronisUpdater.AcronisAPI
{
    public sealed class AcronisAPIAuthenticator : IAcronisAuthenticator
    {
        private string _username;
        private string _password;
        private string _authToken;
        private Uri _baseAuthUri;

        private bool _scopedAuth = false;        
        private Guid _signInTenantId;

        public AcronisAPIAuthenticator(string username, string password, Uri baseAuthUri)
        {
            _baseAuthUri = baseAuthUri;
            _username = username;
            _password = password;            
        }

        public AcronisAPIAuthenticator(string authToken, Guid signInTenantId, Uri baseAuthUri)
        {            
            _baseAuthUri = baseAuthUri;
            _signInTenantId = signInTenantId;
            _authToken = authToken;
            _scopedAuth = true;
        }

        public void Authenticate(IRestClient client, IRestRequest request)
        {
            if (string.IsNullOrEmpty(_authToken) && !_scopedAuth)
            {
                AcquireBearerToken();
            }
            else if(!string.IsNullOrEmpty(_authToken) && _scopedAuth)
            {
                AcquireBearerTokenScopedAuth();
            }

            // Set auth token
            string _authHeader = $"Bearer {_authToken}";
            request.AddOrUpdateParameter("Authorization", _authHeader, ParameterType.HttpHeader);
        }

        // Auth methods
        private void AcquireBearerToken()
        {
            var client = new RestClient(_baseAuthUri);
            var request = new RestRequest("api/2/idp/token", Method.POST);
            request.AddParameter("grant_type", "password");
            request.AddParameter("username", _username);
            request.AddParameter("password", _password);
            
            var response = client.Execute(request);

            if(response.StatusCode != HttpStatusCode.OK)
            {
                if(response.StatusCode == HttpStatusCode.Forbidden)
                    throw new Exception($"Error - Unauthorized. Probably wrong username and/or password? Response content: {response.Content}");
                else
                    throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            string bearerToken = (string)responseJson.SelectToken("access_token");

            if (string.IsNullOrEmpty(bearerToken))
            {
                throw new InvalidOperationException("Authentication Token received by the server is null or empty");
            }

            _authToken = bearerToken;
        }

        private void AcquireBearerTokenScopedAuth()
        {
            if (string.IsNullOrEmpty(_authToken))
                throw new InvalidOperationException("Bearer token is null or empty, but necessary for scoped authentication");

            var client = new RestClient(_baseAuthUri);
            var request = new RestRequest("bc/idp/token", Method.POST);
            //_authToken = "{\"access_token\": \"eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImtpZCI6IjkyNjM1MGY3LWQzMDEtNDgwNi1iZDk2LWZjMDcwMzRjN2Y5NyJ9.eyJhdWQiOiJiNDZmOTNmZC01MTg1LTQ1ZGMtYjVkYS05ZTFkODA1NzA5NWUiLCJleHAiOjE2MTE0NDI2NjIsImlhdCI6MTYxMTQzNTQ2MiwiaXNzIjoiaHR0cHM6Ly9ldTItY2xvdWQuYWNyb25pcy5jb20iLCJzdWIiOiJiNDZmOTNmZC01MTg1LTQ1ZGMtYjVkYS05ZTFkODA1NzA5NWUiLCJ1aWQiOiJiNDZmOTNmZC01MTg1LTQ1ZGMtYjVkYS05ZTFkODA1NzA5NWUiLCJzY29wZSI6W3sidGlkIjoiOTY3Nzc0YzMtMWMyZS00MzE0LWEwMjItZWFiZjI1YmQ5MDEwIiwicm9sZSI6InBhcnRuZXJfYWRtaW4ifV0sInZlciI6MiwianRpIjoieEJLbVd4UmpGU2VueGhKNTRQOTlhaiJ9.T6QJEKTzKVs4ZnFTYRFniW5OFg917ssgZ9TM5iKD2CJn_7ZcauVDyRLEauo265-T8MAYT9bja6RbZCZHJ2FXstnD-hapoY9n6uzTtDse5V8AsafPt_ngtI3jBUagxEiR0csAqnRbHRAMaGai5w2AbfQ3F4XjyFM_osY3ycX4_G-jDSLBryBsYO5EiVpACx0BfaK3L7kdI6fpZVqwk7Q8vUmxrZXcV6GMiRVZWzDik35xpUVSh2xe_0tVVeDWnmHwiTvg5ZUIWKgCObyneWyV1gQgsegWXCgGIZKCOMTU0uLYhabvAj2YwyLrIFZ1YeblsF_UWtCgEe98k5ujCdJZGg\",\"id_token\": \"eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsImtpZCI6IjkyNjM1MGY3LWQzMDEtNDgwNi1iZDk2LWZjMDcwMzRjN2Y5NyJ9.eyJhdWQiOiJiNDZmOTNmZC01MTg1LTQ1ZGMtYjVkYS05ZTFkODA1NzA5NWUiLCJleHAiOjE2MTE0NDI2NjIsImlhdCI6MTYxMTQzNTQ2MiwiaXNzIjoiaHR0cHM6Ly9ldTItY2xvdWQuYWNyb25pcy5jb20iLCJzdWIiOiJiNDZmOTNmZC01MTg1LTQ1ZGMtYjVkYS05ZTFkODA1NzA5NWUiLCJ1aWQiOiJiNDZmOTNmZC01MTg1LTQ1ZGMtYjVkYS05ZTFkODA1NzA5NWUiLCJuYW1lIjoidGstdXBkYXRldGVzdCIsImVtYWlsIjoidGtAZG9nYWRvLmRlIiwidmVyIjoyLCJqdGkiOiJ4QkttV3hSakZTZW54aEo1NFA5OWFqIn0.K4cnKtuEf9MVYEFIu-6GMzs59du0wKDgG16mcJX6s18m6zJP_QTaYXj7IV0qoyaLg8UafNAezY0kgP1LYQrNxdR48HhMV7Oencl5qpcBZnbRsS4LVuQoou5TjugXj8JaAcSQ9hZHL_9qRiB0tpP-xqSfWauPdGoIzZU-DOza9aF0ynwGwPF4oxs531ITlMa67x3rn6yg0t0yJggmWg9shp8dUwiKTw_f-5O4nD8KYR3-NfHJW31M5q38YT5_NZGBdxbJWFp8XlnelVMDFYuEmB8UKOtYqIxFtWUZFjEcKtMBuy_bVmybw2HGviRJ8iVCa23g2Thbu7NE8_MGLE8TEQ\",\"expires_on\": 1611442662,\"token_type\": \"bearer\"}";

            request.AddParameter("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer");
            request.AddParameter("assertion", _authToken);
            request.AddParameter("scope", $"urn:acronis.com:tenant-id:{_signInTenantId}");

            var response = client.Execute(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Error - Received HTTP {response.StatusCode}:{response.StatusDescription}. Response content: {response.Content}");
            }

            JObject responseJson = JObject.Parse(response.Content);
            string bearerToken = (string)responseJson.SelectToken("access_token");

            if (string.IsNullOrEmpty(bearerToken))
            {
                throw new InvalidOperationException("Authentication Token received by the server is null or empty");
            }

            _authToken = bearerToken;
        }

        // Properties
        public String AuthToken
        {
            get => _authToken;
        }
    }
}
