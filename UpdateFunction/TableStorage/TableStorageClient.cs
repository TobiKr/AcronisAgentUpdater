using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Cosmos.Table;
using azuregeek.AZAcronisUpdater.TableStorage.Models;

namespace azuregeek.AZAcronisUpdater.TableStorage
{
    public class TableStorageClient
    {
        private CloudStorageAccount _tableAccount;
        private CloudTableClient _client;
        private CloudTable _table;

        public TableStorageClient(string connectionString)
        {
            // Initialize Table Storage
            _tableAccount = CloudStorageAccount.Parse(connectionString);
            _client = _tableAccount.CreateCloudTableClient();
            _table = _client.GetTableReference("AgentUpdates");

            // create Table
            _table.CreateIfNotExists();
        }

        public async Task<TableResult> InsertAgentUpdateEntity(AgentUpdateEntity entity)
        {
            TableOperation tableOperation = TableOperation.Insert(entity);
            var result = await _table.ExecuteAsync(tableOperation);
            return result;
        }

        public async Task<List<AgentUpdateEntity>> GetTableDataForUpdateRun(string updateRun)
        {
            var condition = TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, updateRun);
            var query = new TableQuery<AgentUpdateEntity>().Where(condition);
            var agentUpdateList = new List<AgentUpdateEntity>();

            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<AgentUpdateEntity> resultSegment = await _table.ExecuteQuerySegmentedAsync(query, token);
                token = resultSegment.ContinuationToken;

                foreach (AgentUpdateEntity entity in resultSegment.Results)
                {
                    agentUpdateList.Add(entity);
                }
            } while (token != null);

            return agentUpdateList;
        }
    }
}
