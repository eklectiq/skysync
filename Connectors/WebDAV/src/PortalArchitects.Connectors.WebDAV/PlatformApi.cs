using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.IO;
using PortalArchitects.Net.Http;

namespace PortalArchitects.Connectors.WebDAV
{
	internal static class PlatformApi
	{
		internal static readonly XNamespace XnsDav = "DAV:";

		internal static string ToRelativeUri(this PlatformItemDefinition item) {
			return item.GetUriEncodedName().TrimStart('/');
		}

		internal static void AddDepthHeader(this HttpRequestMessage request, int depth) {
			if (depth >= 0) {
				request.Headers.Add("Depth", depth > 1 ? "infinity" : depth.ToString());
			}
		}

		internal static string GetName(this XElement element, string baseUrl) {
			var elementUrl = element.Descendants(XnsDav + "href").FirstOrDefault()?.Value;
			if (string.IsNullOrEmpty(elementUrl)) {
				return null;
			}

			var uri = new Uri(new Uri(baseUrl), new Uri(elementUrl, UriKind.RelativeOrAbsolute));
			return uri.Segments.Last().TrimEnd('/');
		}

		internal static DateTimeOffset? AsDateTime(this XElement element, string nodeName) {
			return element.Descendants(XnsDav + nodeName).FirstOrDefault()?.AsDateTime();
		}

		internal static DateTimeOffset? AsDateTime(this XElement element) {
			return DateTimeOffset.TryParse(element.Value, out var value) ? value : (DateTimeOffset?) null;
		}

		internal static long? AsLong(this XElement element, string nodeName) {
			return element.Descendants(XnsDav + nodeName).FirstOrDefault()?.AsLong();
		}

		internal static long? AsLong(this XElement element) {
			return long.TryParse(element.Value, out var value) ? value : (long?) null;
		}

		internal static async Task<CreateItemOperationResult> CreateFolder(this ConnectionState client, CreateContainerOperation operation, CancellationToken token) {
			using (var request = new HttpRequestMessage(WebDavMethod.MkCol, operation.Item.ToRelativeUri())) {
				using (var response = await client.HttpClient.SendAsync(request, token)) {
					switch (response.StatusCode) {
						case HttpStatusCode.Created:
							// folder created continue
							return CreateItemOperationResult.CreatedNewItem(operation.Item);
						case HttpStatusCode.MethodNotAllowed:
						case HttpStatusCode.Forbidden:
							// the folder is there, just ignore it and act like it was created
							return CreateItemOperationResult.ItemAlreadyExisted(operation.Item);
						default:
							response.EnsureSuccessStatusCode();
							return CreateItemOperationResult.Success(operation.Item);
					}
				}
			}
		}

		internal static async Task DeleteItem(this ConnectionState client, DeleteItemOperation operation, CancellationToken token) {
			using (var request = new HttpRequestMessage(HttpMethod.Delete, operation.Item.ToRelativeUri())) {
				using (var response = await client.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden) {
						throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
					}

					response.EnsureSuccessStatusCode();
				}
			}
		}

		internal static async Task<IEnumerable<PlatformItemDefinition>> GetChildren(
			this ConnectionState client, Connection connection, PlatformItemDefinition directory, CancellationToken token) {
			var absoluteUri = client.HttpClient.BaseAddress.AbsoluteUri;
			var children = await client.GetChildrenElements(connection, directory, token);
			return children
				// NOTE First item is the directory itself:
				.Skip(1)
				.Select(element => element.Parse(directory, absoluteUri))
				.ToList();
		}

		internal static async Task<PlatformItemDefinition> GetItem(
			this ConnectionState client, Connection connection, PlatformItemDefinition item, CancellationToken token) {
			XElement element;
			if (item.ItemType == null) {
				try {
					element = await client.GetFileElement(connection, item, token);
				} catch (PlatformItemNotFoundException) {
					element = await client.GetFolderElement(connection, item, token);
				}
			} else {
				element = item.ItemType.Equals(PlatformItemTypes.Directory)
					? await client.GetFolderElement(connection, item, token)
					: await client.GetFileElement(connection, item, token);
			}
			return element.Parse(item.Parent, client.HttpClient.BaseAddress.AbsoluteUri);
		}

		private static async Task<XElement> GetFileElement(this ConnectionState client, Connection connection, PlatformItemDefinition item, CancellationToken token) {
			using (var request = new HttpRequestMessage(WebDavMethod.PropFind, item.ToRelativeUri())) {
				request.AddDepthHeader(item.GetPathDepth());
				using (var response = await client.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden) {
						throw new PlatformItemNotFoundException(connection, item);
					}

					response.EnsureSuccessStatusCode();
					return await response.GetXElementAsync();
				}
			}
		}

		private static async Task<XElement> GetFolderElement(this ConnectionState client, Connection connection, PlatformItemDefinition item, CancellationToken token) {
			var absoluteUri = client.HttpClient.BaseAddress.AbsoluteUri;
			return (await client.GetChildrenElements(connection, item, token))
				// NOTE First item is the directory itself:
				.FirstOrDefault(element => element.GetName(absoluteUri) == item.PlatformName);
		}

		private static async Task<IEnumerable<XElement>> GetChildrenElements(
			this ConnectionState client, Connection connection, PlatformItemDefinition parent, CancellationToken token) {
			using (var request = new HttpRequestMessage(WebDavMethod.PropFind, parent.ToRelativeUri()) {
				Content = new XElement(
					XnsDav + "propfind",
					new XAttribute(XNamespace.Xmlns + "d", XnsDav),
					new XElement(
						XnsDav + "prop",
						new XElement(XnsDav + "displayname"),
						new XElement(XnsDav + "creationdate"),
						new XElement(XnsDav + "getlastmodified"),
						new XElement(XnsDav + "getcontentlength"),
						new XElement(XnsDav + "getcontenttype"),
						new XElement(XnsDav + "getetag"),
						new XElement(XnsDav + "resourcetype"))).ToContent()
			}) {
				request.AddDepthHeader(parent.GetPathDepth());
				using (var response = await client.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Forbidden) {
						throw new PlatformItemNotFoundException(connection, parent);
					}

					response.EnsureSuccessStatusCode();
					return (await response.GetXElementAsync()).Descendants(XnsDav + "response");
				}
			}
		}

		private static PlatformItemDefinition Parse(this XElement element, PlatformItemDefinition parent, string absoluteUri) {
			return new PlatformItemDefinition {
				ItemType = element.Descendants(XnsDav + "collection").Any()
					? PlatformItemTypes.Directory
					: PlatformItemTypes.File,
				PlatformName = element.GetName(absoluteUri),
				Parent = parent,
				PlatformDisplayName = element.Descendants(XnsDav + "displayname").FirstOrDefault()?.Value,
				CreatedOn = element.AsDateTime("creationdate"),
				ModifiedOn = element.AsDateTime("getlastmodified"),
				Bytes = element.AsLong("getcontentlength") ?? 0
			};
		}

		public static async Task<NativeTransferResult> TransferItem(
			this ConnectionState client, PlatformItemDefinition item, PlatformItemDefinition destination, bool isCopy, CancellationToken token) {
			var method = isCopy ? WebDavMethod.Copy : WebDavMethod.Move;
			for (var retry = 0; retry < 2; retry++) {
				using (var request = new HttpRequestMessage(method, item.ToRelativeUri()) {
					Headers = {
						{ "Destination", new Uri(client.HttpClient.BaseAddress, destination.ToRelativeUri()).AbsoluteUri },
						{ "Overwrite", retry > 0 ? "T" : "True" },
						{ "Depth", "infinity" }
					}
				}) {
					using (var response = await client.HttpClient.SendAsync(request, token)) {
						if (response.StatusCode != HttpStatusCode.BadRequest) {
							response.EnsureSuccessStatusCode();
							return NativeTransferResult.Success(destination);
						}
					}
				}
			}

			return NativeTransferResult.Unsupported();
		}

		internal static Task<IByteSource> ReadItem(this ConnectionState client, Connection connection, PlatformItemDefinition item, CancellationToken token) {
			var request = new HttpRequestMessage(HttpMethod.Get, item.ToRelativeUri());
			return client.HttpClient.ReadAsByteSourceAsync(request, response => {
				if (response.StatusCode == HttpStatusCode.NotFound) {
					throw new PlatformItemNotFoundException(connection, item);
				}

				response.EnsureSuccessStatusCode();
				return Task.CompletedTask;
			}, token);
		}

		internal static Task<CreateItemOperationResult> WriteItem(this ConnectionState client, WriteItemOperation operation, CancellationToken token) {
			var item = operation.Item;
			var connection = operation.Connection;
			return TimeoutHandlers.WithTimeout(async timeoutHandler => {
				var contentType = string.IsNullOrWhiteSpace(item.ContentType)
					? ContentTypes.GetContentTypeByExtension(FileNameParser.GetExtension(item.PlatformName))
					: item.ContentType;
				var content = new ByteSourceContent(operation.Content, timeoutHandler, operation.BufferManager, operation.WriteHandler) {
					Headers = {
						ContentType = new MediaTypeHeaderValue(contentType)
					}
				};
				using (var request = new HttpRequestMessage(HttpMethod.Put, item.ToRelativeUri()) {
					Content = content
				}) {
					request.SetMessageProperty(timeoutHandler);
					using (var response = await client.HttpClient.SendAsync(request, timeoutHandler.Token)) {
						if (response.StatusCode == HttpStatusCode.Conflict) {
							throw new PlatformItemOutOfDateException(connection, item);
						}

						response.EnsureSuccessStatusCode();
					}
				}

				return CreateItemOperationResult.Success(item);
			}, operation, token);
		}

		private static class WebDavMethod
		{
			public static readonly HttpMethod PropFind = new HttpMethod("PROPFIND");
			public static readonly HttpMethod MkCol = new HttpMethod("MKCOL");
			public static readonly HttpMethod Copy = new HttpMethod("COPY");
			public static readonly HttpMethod Move = new HttpMethod("MOVE");
		}
	}
}
