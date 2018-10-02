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
	internal class CreateContainerOperationExecutor : PipelineExecutorBase<CreateContainerOperation, CreateItemOperationResult>
	{
		private readonly ConnectionState connectionState;

		public CreateContainerOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override async Task<CreateItemOperationResult> ExecuteAsync(CreateContainerOperation operation, CancellationToken token) {
			var requestBody = new JObject {
				{ "name", operation.Item.PlatformDisplayName ?? operation.Item.PlatformName },
				{ "folder", new JObject() }
			};

			connectionState.PreserveTimestamps(operation, requestBody);

			using (var request = new HttpRequestMessage(HttpMethod.Post, operation.GetSiblingsRelativeUri().AppendQueryString("nameConflict", "fail")) {
				Content = new JObjectContent(requestBody)
			}) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Created || response.StatusCode == HttpStatusCode.Conflict) {
						return CreateItemOperationResult.ItemAlreadyExisted(await connectionState.GetItem(new GetInfoByPathOperation {
							Item = operation.Item
						}, token));
					}

					await response.EnsureResponseSuccess();
					return CreateItemOperationResult.CreatedNewItem(connectionState.ParseItem(await response.GetJObjectAsync(), operation.Item, false));
				}
			}
		}
	}
}
