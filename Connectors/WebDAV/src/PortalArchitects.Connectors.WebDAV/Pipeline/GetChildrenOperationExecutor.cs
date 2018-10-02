using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class GetChildrenOperationExecutor : PipelineExecutorBase<GetChildrenOperation, IEnumerable<PlatformItemDefinition>> 
	{
		private readonly ConnectionState client;

		internal GetChildrenOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override Task<IEnumerable<PlatformItemDefinition>> ExecuteAsync(GetChildrenOperation operation, CancellationToken token) {
			return client.GetChildren(operation.Connection, operation.Item, token); 
		}
	}
}