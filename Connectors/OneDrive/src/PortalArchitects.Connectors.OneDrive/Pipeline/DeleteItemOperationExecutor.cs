using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class DeleteItemOperationExecutor : PipelineExecutorBase<DeleteItemOperation, PlatformItemDefinition>
	{
		private readonly ConnectionState connectionState;

		public DeleteItemOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override async Task<PlatformItemDefinition> ExecuteAsync(DeleteItemOperation operation, CancellationToken token) {
			return await connectionState.DeleteItem(operation, token);
		}
	}
}
