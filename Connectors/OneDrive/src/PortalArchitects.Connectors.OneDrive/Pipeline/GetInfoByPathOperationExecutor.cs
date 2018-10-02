using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class GetInfoByPathOperationExecutor : PipelineExecutorBase<GetInfoByPathOperation, PlatformItemDefinition>
	{
		private readonly ConnectionState connectionState;

		public GetInfoByPathOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override Task<PlatformItemDefinition> ExecuteAsync(GetInfoByPathOperation operation, CancellationToken token) {
			return connectionState.GetItem(operation, token);
		}
	}
}