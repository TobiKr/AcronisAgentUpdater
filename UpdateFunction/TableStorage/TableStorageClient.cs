using System.Threading.Tasks;
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
    }
}
