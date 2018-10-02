using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Net.Http;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class NativeMoveOperationExecutor : PipelineExecutorBase<NativeMoveOperation, NativeTransferResult>
	{
		private readonly ConnectionState connectionState;

		public NativeMoveOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override async Task<NativeTransferResult> ExecuteAsync(NativeMoveOperation operation, CancellationToken token) {

			using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), operation.GetItemRelativeUri()) {
				Content = new JObjectContent(new JObject {
					{ "parentReference", operation.GetParentReference() }
				})
			}) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
						throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
					}
					await response.EnsureValidPlatformID(() => new PlatformItemNotFoundException(operation.Connection, operation.Item));
					await response.EnsureResponseSuccess();
					operation.Item.Parent = operation.Parent;
					return NativeTransferResult.Success(connectionState.ParseItem(await response.GetJObjectAsync(), operation.Item, false));
				}
			}
		}
	}
}