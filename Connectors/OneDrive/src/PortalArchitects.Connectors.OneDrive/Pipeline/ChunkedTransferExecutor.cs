using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.Pipeline;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.IO;
using PortalArchitects.Net.Http;
using PortalArchitects.Threading;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class ChunkedTransferExecutor : HttpChunkedUploader
	{
		// NOTE: https://dev.onedrive.com/items/upload.htm
		// ReSharper disable once InconsistentNaming
		private const int MB4 = 4 * 1024 * 1024;

		// ReSharper disable once InconsistentNaming
		private const int MB10 = 10 * 1024 * 1024;
		
		// ReSharper disable once InconsistentNaming
		private const int MB50 = 50 * 1024 * 1024;
		
		private readonly ConnectionState connectionState;
		private readonly WriteItemOperation operation;
		private string uploadUrl;
		private string nextExpectedRanges;

		public ChunkedTransferExecutor(ConnectionState connectionState, WriteItemOperation operation, IConnectionExecutionContext context)
			: base(connectionState.HttpClient, operation.Content, operation.WriteHandler) {
			this.connectionState = connectionState;
			this.operation = operation;

			BufferManager = operation.BufferManager;
			TimeoutHandlerProvider = operation;
			RateLimiter = context?.RateLimiter;
			ExceptionMapper = context?.ExceptionMapper;
			RecoveryPolicy = context?.RecoveryPolicy;
		}

		protected override long ChunkSize => MB10;

		protected override long MaxSize => connectionState.SupportMultiPartUpload ? MB50 : MB4;

		protected override bool IsUsingChunkedUpload => TotalSize.HasValue && base.IsUsingChunkedUpload;

		protected override bool RateLimitInitialize => IsUsingChunkedUpload;

		protected override bool RateLimitComplete => operation.PreserveTimestamps;

		protected override async Task Initialize(CancellationToken token) {
			if (!IsUsingChunkedUpload) {
				return;
			}

			var requestUri = operation.GetParentRelativeUri().AppendIfMissing(":") + "/" + operation.Item.PlatformName + ":/upload.createSession";
			using (var request = new HttpRequestMessage(HttpMethod.Post, requestUri) {
				Content = new JObjectContent(new JObject {{
					"item", new JObject {
						{"@name.conflictBehavior", "replace"},
						{"name", operation.Item.PlatformName}
					}
				}})
			}) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					await response.EnsureResponseSuccess();
					var result = await response.GetJObjectAsync();
					uploadUrl = result.Value<string>("uploadUrl");
					nextExpectedRanges = result.Value<JArray>("nextExpectedRanges").FirstOrDefault()?.Value<string>();
				}
			}
		}

		protected override HttpRequestMessage CreateChunkRequest(ChunkDescription pendingChunk) {
			return new HttpRequestMessage(HttpMethod.Put, uploadUrl);
		}

		protected override HttpContent CreateRequestContent(IByteSource byteSource, ChunkDescription pendingChunk, ITimeoutHandler timeoutHandler) {
			var content = base.CreateRequestContent(byteSource, pendingChunk, timeoutHandler);
			// ReSharper disable once PossibleInvalidOperationException
			var length = (long)TotalSize;
			var to = pendingChunk.IsLast ? length - 1 : pendingChunk.Offset + pendingChunk.Size - 1;
			content.Headers.ContentRange = new ContentRangeHeaderValue(pendingChunk.Offset, to, length);
			return content;
		}

		protected override Task EnsureResponseSuccess(HttpResponseMessage response) {
			return response.EnsureResponseSuccess();
		}

		protected override async Task<ChunkDescription> ParseChunkResponse(HttpResponseMessage response, ChunkDescription previousChunk, CancellationToken token) {
			await response.EnsureResponseSuccess();
			var result = await response.GetJObjectAsync();
			if (previousChunk.IsLast) {
				connectionState.ParseItem(result, operation.Item, false);
			} else {
				nextExpectedRanges = result.Value<JArray>("nextExpectedRanges")?.FirstOrDefault()?.Value<string>();
				var from = nextExpectedRanges?.Split('-').First();
				if (long.TryParse(from, NumberStyles.None, null, out var value)) {
					previousChunk.Offset = value;
				}
			}
			return previousChunk;
		}

		protected override async Task CompleteChunkUpload(ChunkDescription lastChunk, ITimeoutHandler timeoutHandler) {
			if (operation.PreserveTimestamps) {
				var body = new JObject();
				connectionState.PreserveTimestamps(operation, body);
				using (var request = new HttpRequestMessage(new HttpMethod("PATCH"), operation.GetItemRelativeUri()) {
					Content = new JObjectContent(body)
				}) {
					using (var response = await connectionState.HttpClient.SendAsync(request, timeoutHandler.Token)) {
						if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
							throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
						}
						await response.EnsureValidPlatformID(() => new PlatformItemNotFoundException(operation.Connection, operation.Item));
						await response.EnsureResponseSuccess();
						connectionState.ParseItem(await response.GetJObjectAsync(), operation.Item, false);
					}
				}
			}
		}

		protected override Task Upload(CancellationToken token) {
			return connectionState.SupportMultiPartUpload ? MultiPartUpload(token) : SingleUpload(token);
		}

		private Task SingleUpload(CancellationToken token) {
			return TimeoutHandlers.WithTimeout(async timeoutHandler => {
				using (var request = new HttpRequestMessage(HttpMethod.Put, $"{operation.GetItemRelativeUri()}/content") {
					Content = new ByteSourceContent(Source, timeoutHandler, BufferManager, WriteHandler)
				}) {
					using (var response = await HttpClient.SendAsync(request, token)) {
						return await CompleteUpload(response);
					}
				}
			}, TimeoutHandlerProvider, TimeoutManager, token);
		}

		private Task MultiPartUpload(CancellationToken token) {
			return TimeoutHandlers.WithTimeout(async timeoutHandler => {
				if (!operation.Item.IsNew()) {
					// NOTE OneDrive doesn't currently support replace on multipart uploads
					await connectionState.DeleteItem(new DeleteItemOperation {
						Item = operation.Item
					}, token);
				}
				var fileContent = new ByteSourceContent(operation.Content, timeoutHandler, operation.WriteHandler) {
					Headers = {
						{ "Content-ID", "<content>" }
					}
				};
				var metadataBody = new JObject {
					{ "name", operation.Item.PlatformName },
					{ "file", new JObject() },
					{ "@content.sourceUrl", "cid:content" },
					{ "@name.conflictBehavior", "fail" }
				};
				connectionState.PreserveTimestamps(operation, metadataBody);
				var metadataContent = new JObjectContent(metadataBody) {
					Headers = {
						{ "Content-ID", "<metadata>" }
					}
				};
				var boundary = Guid.NewGuid().ToString();
				var content = new UnencodedMultipartFormDataContent(fileContent, boundary) {
					metadataContent,
					fileContent
				};
				content.Headers.ContentType = new MediaTypeHeaderValue("multipart/related") {
					Parameters = {
						new NameValueHeaderValue("boundary", boundary)
					}
				};

				using (var request = new HttpRequestMessage(HttpMethod.Post, operation.GetSiblingsRelativeUri()) {
					Content = content
				}) {
					request.SetRequestIsSensitive();
					using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
						return await CompleteUpload(response);
					}
				}
			}, operation, token);
		}

		private async Task<PlatformItemDefinition> CompleteUpload(HttpResponseMessage response) {
			await response.EnsureResponseSuccess();
			var obj = await response.GetJObjectAsync();
			connectionState.ParseItem(obj, operation.Item, false);
			return operation.Item;
		}
	}
}
