using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.IO;
using PortalArchitects.Net.Http;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class ReadItemOperationExecutor : PipelineExecutorBase<ReadItemOperation, IByteSource>
	{
		private readonly ConnectionState connectionState;

		public ReadItemOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override Task<IByteSource> ExecuteAsync(ReadItemOperation operation, CancellationToken token) {
			var request = new HttpRequestMessage(HttpMethod.Get, operation.GetItemRelativeUri() + "/content")
				.SetResponseIsSensitive();

			return connectionState.HttpClient.ReadAsByteSourceAsync(request, async response => {
				if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
					throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
				}
				await response.EnsureValidPlatformID(() => new PlatformItemNotFoundException(operation.Connection, operation.Item));
				await response.EnsureResponseSuccess();
			}, token);
		}
	}
}
