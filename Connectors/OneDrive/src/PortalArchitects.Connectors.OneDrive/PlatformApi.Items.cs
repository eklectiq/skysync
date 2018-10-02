using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Net.Http;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Serialization;
using System.Linq;

namespace PortalArchitects.Connectors.OneDrive
{
	internal static partial class PlatformApi
	{
		internal static PlatformItemDefinition ParseItem(this ConnectionState connectionState, JToken value, PlatformItemDefinition item, bool parseParentPath) {
			item.PlatformID = value.Value<string>("id");
			item.PlatformName = value.Value<string>("name");
			item.PlatformDisplayName = value.Value<string>("displayName");

			if (parseParentPath && item.Parent == null) {
				item.Parent = value["parentReference"]?.ParseParent();
			}

			var file = value["file"];
			if (file != null) {
				item.ItemType = PlatformItemTypes.File;
			} else {
				item.ItemType = PlatformItemTypes.Directory;
				item.AllowedItemTypes = connectionState.AllowedItemTypes;
			}

			item.Bytes = value.Value<long>("size");
			item.Hash = file?["hashes"]?.Value<string>("sha1Hash");
			item.ContentType = file?.Value<string>("mimeType");

			item.CreatedBy = value["createdBy"]?["user"]?.ParseAccount();
			item.CreatedOn = DateTimeParser.ParseDateTime(value.Value<string>("createdDateTime"))?.ToUniversalTime();
			item.ContentCreatedOn = DateTimeParser.ParseDateTime(value["fileSystemInfo"]?.Value<string>("createdDateTime"))?.ToUniversalTime();

			item.ModifiedBy = value["lastModifiedBy"]?["user"]?.ParseAccount();
			item.ModifiedOn = DateTimeParser.ParseDateTime(value.Value<string>("lastModifiedDateTime"))?.ToUniversalTime();
			item.ContentModifiedOn = DateTimeParser.ParseDateTime(value["fileSystemInfo"]?.Value<string>("lastModifiedDateTime"))?.ToUniversalTime();

			return item;
		}

		internal static IEnumerable<PlatformItemDefinition> ParseItems(this ConnectionState connectionState, PlatformItemDefinition parent, JArray value) {
			return value.Select(x => connectionState.ParseItem(x, new PlatformItemDefinition {
				Parent = parent
			}, parent == null));
		}

		internal static PlatformItemDefinition ParseParent(this JToken value) {
			var path = value.Value<string>("path");
			const string driveRoot = "/drive/root:";
			if (path.StartsWith(driveRoot, StringComparison.OrdinalIgnoreCase)) {
				path = path.Substring(driveRoot.Length);
			}
			var segments = Path.SplitSegments(path);
			var parent = segments.Aggregate<string, PlatformItemDefinition>(null, (current, segment) => new PlatformItemDefinition {
				PlatformName = segment,
				ItemType = PlatformItemTypes.Directory,
				Parent = current
			});
			if (parent != null) {
				parent.PlatformID = value.Value<string>("id");
			}
			return parent;
		}

		internal static async Task<PlatformItemDefinition> GetItem(this ConnectionState connectionState, GetInfoByPathOperation operation, CancellationToken token) {
			using (var request = new HttpRequestMessage(HttpMethod.Get, operation.GetItemRelativeUri())) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
						throw new PlatformItemNotFoundException(operation.Connection, operation.Item ?? Path.Parse(operation.GetFullPath()));
					}
					await response.EnsureResponseSuccess();
					return connectionState.ParseItem(await response.GetJObjectAsync(), operation.Item ?? new PlatformItemDefinition(), true);
				}
			}
		}

		internal static void PreserveTimestamps(this ConnectionState connectionState, ContentOperation operation, JObject requestBody) {
			if (operation.PreserveTimestamps && connectionState.Features.IsAllowTimestampPreservation) {
				var fileSystemInfo = new JObject();
				if (operation.CreatedOn.IsValidDateTime()) {
					fileSystemInfo.Add("createdDateTime", operation.CreatedOn.ToIso8601());
				}
				if (operation.ModifiedOn.IsValidDateTime()) {
					fileSystemInfo.Add("lastModifiedDateTime", operation.ModifiedOn.ToIso8601());
				}
				if (fileSystemInfo.HasValues) {
					requestBody.Add("fileSystemInfo", fileSystemInfo);
				}
			}
		}

		internal static async Task<PlatformItemDefinition> DeleteItem(this ConnectionState connectionState, DeleteItemOperation operation, CancellationToken token) {
			using (var request = new HttpRequestMessage(HttpMethod.Delete, operation.GetItemRelativeUri())) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
						throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
					}
					await response.EnsureResponseSuccess();
					return operation.Item;
				}
			}
		}
	}
}