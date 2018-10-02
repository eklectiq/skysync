using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Pipeline;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class WriteItemOperationExecutor : PipelineExecutorBase<WriteItemOperation, CreateItemOperationResult>
	{
		private readonly ConnectionState connectionState;
		private readonly IConnectionExecutionContextAccessor contextAccessor;

		internal WriteItemOperationExecutor(ConnectionState connectionState, IConnectionExecutionContextAccessor contextAccessor) {
			this.connectionState = connectionState;
			this.contextAccessor = contextAccessor;
		}

		public override async Task<CreateItemOperationResult> ExecuteAsync(WriteItemOperation operation, CancellationToken token) {
			await new ChunkedTransferExecutor(connectionState, operation, contextAccessor?.Current).ExecuteAsync(token);
			return CreateItemOperationResult.Success(operation.Item);
		}
	}
}
