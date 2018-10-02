using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Net.Http;
using PortalArchitects.Runtime.Pipeline;
using System.Linq;
using PortalArchitects.Data;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class PageChildrenOperationExecutor : PipelineExecutorBase<PageChildrenOperation, PagedData<PlatformItemDefinition>>
	{
		private readonly ConnectionState connectionState;

		internal PageChildrenOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override async Task<PagedData<PlatformItemDefinition>> ExecuteAsync(PageChildrenOperation operation, CancellationToken token) {
			var uri = operation.GetChildrenRelativeUri();
			var page = operation.Page;
			if (page != null) {
				if (page.PageSize.HasValue) {
					uri = uri.AppendQueryString("top", page.PageSize);
				}

				if (!string.IsNullOrWhiteSpace(page.PageToken)) {
					uri = uri.AppendQueryString("skiptoken", page.PageToken);
				}

				if (page.OrderBy?.Any() == true) {
					var orderBys = new List<string>();
					foreach (var sortDescription in page.OrderBy) {
						if (sortDescription.PropertyName.Equals(PlatformApi.GetDataPropertyName(() => operation.Item.PlatformName), StringComparison.OrdinalIgnoreCase)) {
							orderBys.Add("name " + sortDescription.GetDirection());
						}
						if (sortDescription.PropertyName.Equals(PlatformApi.GetDataPropertyName(() => operation.Item.Bytes), StringComparison.OrdinalIgnoreCase)) {
							orderBys.Add("size " + sortDescription.GetDirection());
						}
						if (sortDescription.PropertyName.Equals(PlatformApi.GetDataPropertyName(() => operation.Item.ModifiedOn), StringComparison.OrdinalIgnoreCase)) {
							orderBys.Add("lastModifiedDateTime " + sortDescription.GetDirection());
						}
					}
					uri = uri.AppendQueryString("orderby", string.Join(",", orderBys));
				}
			}

			using (var request = new HttpRequestMessage(HttpMethod.Get, uri)) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
						throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
					}
					await response.EnsureResponseSuccess();
					var result = await response.GetJObjectAsync();
					var pageToken = result.ParseToken();
					return new PagedData<PlatformItemDefinition> {
						Page = new PageDescription {
							PageSize = page?.PageSize,
							PageToken = pageToken
						},
						Data = new DataList<PlatformItemDefinition>(connectionState.ParseItems(operation.Item, result.Value<JArray>("value"))),
						HasMore = !string.IsNullOrWhiteSpace(pageToken)
					};
				}
			}
		}
	}
}