using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Net.Http;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class GetInfoByIDOperationExecutor : PipelineExecutorBase<GetInfoByIDOperation, PlatformItemDefinition>
	{
		private readonly ConnectionState connectionState;

		public GetInfoByIDOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override async Task<PlatformItemDefinition> ExecuteAsync(GetInfoByIDOperation operation, CancellationToken token) {
			using (var request = new HttpRequestMessage(HttpMethod.Get, operation.GetItemRelativeUri())) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
						throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
					}
					await response.EnsureValidPlatformID(() => new PlatformItemNotFoundException(operation.Connection, operation.Item));
					await response.EnsureResponseSuccess();
					return connectionState.ParseItem(await response.GetJObjectAsync(), operation.Item ?? new PlatformItemDefinition(), true);
				}
			}
		}
	}
}