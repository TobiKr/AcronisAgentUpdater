using System;
using Microsoft.Azure.Cosmos.Table;

namespace azuregeek.AZAcronisUpdater.TableStorage.Models
{
    public class AgentUpdateEntity : TableEntity
    {
        public AgentUpdateEntity(string updateRunDateTime, Guid agentID)
        {
            PartitionKey = updateRunDateTime;
            RowKey = agentID.ToString();
        }
        public AgentUpdateEntity()
        {
            
        }

        // Properties
        public string TenantID { get; set; }
        public string TenantName { get; set; }
        public string ParentTenantID { get; set; }
        public string ParentTenantName { get; set; }
        public string AgentID { get; set;}
        public string HostName { get; set; }
        public string AgentVersionBeforeUpdate { get; set; }
        public string AgentVersionAfterUpdate { get; set; }
        public string UpdateActivityID { get; set; }
        public string AgentOS { get; set; }
    }
}
