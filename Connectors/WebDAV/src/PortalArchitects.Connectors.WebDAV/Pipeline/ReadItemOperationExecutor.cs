using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.IO;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV.Pipeline
{
	internal class ReadItemOperationExecutor : PipelineExecutorBase<ReadItemOperation, IByteSource>
	{
		private readonly ConnectionState client;

		internal ReadItemOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override Task<IByteSource> ExecuteAsync(ReadItemOperation operation, CancellationToken token) {
			return client.ReadItem(operation.Connection, operation.Item, token);
		}
	}
}