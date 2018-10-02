using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class GetInfoByPathOperationExecutor : PipelineExecutorBase<GetInfoByPathOperation, PlatformItemDefinition>
	{
		private readonly ConnectionState client;

		public GetInfoByPathOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override Task<PlatformItemDefinition> ExecuteAsync(GetInfoByPathOperation operation, CancellationToken token) {
			return client.GetItem(operation.Connection, operation.Item, token);
		}
	}
}
