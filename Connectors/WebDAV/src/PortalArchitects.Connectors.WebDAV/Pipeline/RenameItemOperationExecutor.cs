using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class RenameItemOperationExecutor : PipelineExecutorBase<RenameItemOperation, PlatformItemDefinition>
	{
		private readonly ConnectionState client;

		internal RenameItemOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override async Task<PlatformItemDefinition> ExecuteAsync(RenameItemOperation operation, CancellationToken token) {
			await client.TransferItem(operation.Item, new PlatformItemDefinition {
				Parent = operation.Item.Parent,
				PlatformName = operation.DesiredName
			}, false, token);
			operation.Item.PlatformName = operation.DesiredName;
			return operation.Item;
		}
	}
}