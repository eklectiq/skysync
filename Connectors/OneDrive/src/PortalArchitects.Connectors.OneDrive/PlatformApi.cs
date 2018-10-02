using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.Security;
using PortalArchitects.Net.Http;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.Pipeline;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Data;

namespace PortalArchitects.Connectors.OneDrive
{
	internal static partial class PlatformApi
	{
		internal static async Task<JToken> LoadDefaultDrive(this ConnectionState connectionState, CancellationToken token) {
			using (var response = await connectionState.HttpClient.GetAsync("drive", token)) {
				await response.EnsureResponseSuccess();
				return await response.GetJObjectAsync();
			}
		}

		internal static StorageQuota ParseQuota(this JToken value) {
			return new StorageQuota {
				Quota = value.Value<int>("total"),
				Used = value.Value<int>("used")
			};
		}

		internal static AccountDefinition ParseAccount(this JToken value) {
			return new AccountDefinition {
				ID = value.Value<string>("id"),
				FullName = value.Value<string>("displayName")
			};
		}

		internal static bool IsNew(this PlatformItemDefinition item) {
			return string.IsNullOrWhiteSpace(item.PlatformID);
		}

		private static string GetItemRelativeUri(this IEnumerable<string> segments) {
			return "drive/root:" + segments.GetUriEncodedName() + ":";
		}

		private static string GetItemRelativeUri(this string platformID) {
			return "drive/items/" + platformID;
		}

		private static string GetItemRelativeUri(PlatformItemDefinition item) {
			string relativeUri = null;
			if (item != null) {
				relativeUri = item.IsNew()
					? item.GetSegments().GetItemRelativeUri()
					: item.PlatformID.GetItemRelativeUri();
			}
			return relativeUri ?? "drive/root";
		}

		internal static string GetItemRelativeUri(this IItemOperation operation) {
			return GetItemRelativeUri(operation.Item);
		}

		internal static string GetItemRelativeUri(this GetInfoByPathOperation operation) {
			return (operation.Item?.GetSegments() ?? operation.Segments)?.GetItemRelativeUri();
		}

		internal static string GetItemRelativeUri(this GetInfoByIDOperation operation) {
			return operation.GetPlatformID().GetItemRelativeUri();
		}

		internal static string GetParentRelativeUri(this IItemOperation operation) {
			return GetItemRelativeUri(operation.Item?.Parent);
		}

		internal static string GetSiblingsRelativeUri(this IItemOperation operation) {
			return operation.GetParentRelativeUri() + "/children";
		}

		internal static string GetChildrenRelativeUri(this IItemOperation operation) {
			return GetItemRelativeUri(operation.Item) + "/children";
		}

		internal static JObject GetParentReference(this NativeTransferOperation operation) {
			var parentReference = new JObject();
			if (string.IsNullOrEmpty(operation.Parent.PlatformID)) {
				parentReference.Add("path", "/" + operation.Parent.GetSegments().GetItemRelativeUri());
			} else {
				parentReference.Add("id", operation.Parent.PlatformID);
			}
			return parentReference;
		}

		internal static string GetDirection(this SortDescription sortDescription) {
			return sortDescription.IsAscending ? "asc" : "desc";
		}

		internal static TType ParseEnum<TType>(string value) {
			return (TType)Enum.Parse(typeof(TType), value);
		}

		internal static string GetDataPropertyName<TMemberType>(Expression<Func<TMemberType>> expr) {
			var propertyInfo = (expr.Body as MemberExpression)?.Member as PropertyInfo;
			return propertyInfo?.GetCustomAttribute<DataMemberAttribute>(true).Name ?? propertyInfo?.Name;
		}

		internal static string ParseToken(this JObject result) {
			var pageTokenValue = result.Value<string>("@odata.nextLink");
			return pageTokenValue != null ? new Uri(pageTokenValue).ParseQueryString()["$skiptoken"] : null;
		}

		public static string AppendIfMissing(this string url, string substr) {
			if (!url.EndsWith(substr)) {
				url += substr;
			}
			return url;
		}
	}
}