using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class CreateContainerOperationExecutor : PipelineExecutorBase<CreateContainerOperation, CreateItemOperationResult>
	{
		private readonly ConnectionState client;

		internal CreateContainerOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override Task<CreateItemOperationResult> ExecuteAsync(CreateContainerOperation operation, CancellationToken token) {
			return client.CreateFolder(operation, token);
		}
	}
}
