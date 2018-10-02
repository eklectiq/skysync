using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class NativeTransferOperationExecutor : PipelineExecutorBase<NativeTransferOperation, NativeTransferResult>
	{
		private readonly ConnectionState client;

		internal NativeTransferOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override async Task<NativeTransferResult> ExecuteAsync(NativeTransferOperation operation, CancellationToken token) {
			return await client.TransferItem(operation.Item, new PlatformItemDefinition {
				PlatformName = operation.Item.PlatformName,
				Parent = operation.Parent
			}, operation is NativeCopyOperation, token);
		}
	}
}