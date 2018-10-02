using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class WriteItemOperationExecutor : PipelineExecutorBase<WriteItemOperation, CreateItemOperationResult>
	{
		private readonly ConnectionState client;

		internal WriteItemOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override Task<CreateItemOperationResult> ExecuteAsync(WriteItemOperation operation, CancellationToken token) {
			return client.WriteItem(operation, token);
		}
	}
}
