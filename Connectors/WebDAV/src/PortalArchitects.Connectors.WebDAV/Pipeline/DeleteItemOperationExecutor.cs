using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class DeleteItemOperationExecutor : PipelineExecutorBase<DeleteItemOperation, PlatformItemDefinition>
	{
		private readonly ConnectionState client;

		internal DeleteItemOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override async Task<PlatformItemDefinition> ExecuteAsync(DeleteItemOperation operation, CancellationToken token) {
			await client.DeleteItem(operation, token);
			return operation.Item;
		}
	}
}