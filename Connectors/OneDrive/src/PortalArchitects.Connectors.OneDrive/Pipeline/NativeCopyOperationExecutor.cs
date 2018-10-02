using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Net.Http;
using PortalArchitects.Runtime.Pipeline;
using PortalArchitects.Threading;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class NativeCopyOperationExecutor : PipelineExecutorBase<NativeCopyOperation, NativeTransferResult>
	{
		private readonly ConnectionState connectionState;

		public NativeCopyOperationExecutor(ConnectionState connectionState) {
			this.connectionState = connectionState;
		}

		public override async Task<NativeTransferResult> ExecuteAsync(NativeCopyOperation operation, CancellationToken token) {
			var monitorUrl = await Begin(operation, token);
			var retryBackoffCalculator = new RetryBackoffCalculator(connectionState.RetryInterval);
			return await RetryHelper.Retry(
				() => Execute(monitorUrl, operation.Parent, retryBackoffCalculator, token),
				connectionState.RetryCount,
				connectionState.RetryInterval,
				token, 
				IsMonitorException);
		}

		private async Task<Uri> Begin(NativeTransferOperation operation, CancellationToken token) {
			using (var request = new HttpRequestMessage(HttpMethod.Post, operation.GetItemRelativeUri() + "/action.copy") {
				Headers = {
					{"Prefer", "respond-async"}
				},
				Content = new JObjectContent(
					new JObject {
						{"parentReference", operation.GetParentReference()}
					})
			}) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Gone) {
						throw new PlatformItemNotFoundException(operation.Connection, operation.Item);
					}
					await response.EnsureValidPlatformID(() => new PlatformItemNotFoundException(operation.Connection, operation.Item));
					await response.EnsureResponseSuccess();
					Debug.Assert(response.StatusCode == HttpStatusCode.Accepted);
					return response.Headers.Location;
				}
			}
		}

		private async Task<NativeTransferResult> Execute(Uri monitorUrl, PlatformItemDefinition parent, RetryBackoffCalculator retryBackoffCalculator, CancellationToken token) {
			var locationUrl = await Monitor(monitorUrl, retryBackoffCalculator, token);
			return await Complete(parent, token, locationUrl);
		}

		private async Task<Uri> Monitor(Uri monitorUrl, RetryBackoffCalculator retryBackoffCalculator, CancellationToken token) {
			Uri locationUrl;
			using (var request = new HttpRequestMessage(HttpMethod.Get, monitorUrl)) {
				using (var response = await connectionState.HttpClientNoRedirect.SendAsync(request, token)) {
					if (response.StatusCode == HttpStatusCode.Accepted) {
						var result = await response.GetJObjectAsync();
						throw retryBackoffCalculator.RetryNext(PlatformApi.ParseEnum<MonitorStatus>(result.Value<string>("status")), result.Value<float>("percentageComplete"));
					}

					if (response.StatusCode != HttpStatusCode.RedirectMethod) {
						await response.EnsureResponseSuccess();
						throw new InvalidOperationException();
					}

					locationUrl = response.Headers.Location;
				}
			}
			return locationUrl;
		}

		private async Task<NativeTransferResult> Complete(PlatformItemDefinition parent, CancellationToken token, Uri locationUrl) {
			using (var request = new HttpRequestMessage(HttpMethod.Get, locationUrl)) {
				using (var response = await connectionState.HttpClient.SendAsync(request, token)) {
					await response.EnsureResponseSuccess();
					return NativeTransferResult.Success(connectionState.ParseItem(await response.GetJObjectAsync(), new PlatformItemDefinition {
						Parent = parent
					}, false));
				}
			}
		}

		private static bool IsMonitorException(Exception e) {
			return e is RetryException;
		}

		private class RetryException : Exception
		{
		}

		private class RetryMonitorException : RetryException, ISpecifyRetryTimeout
		{
			public RetryMonitorException(TimeSpan? retryIn) {
				RetryIn = retryIn;
			}

			public TimeSpan? RetryIn {
				get;
			}
		}

		private class RetryBackoffCalculator
		{
			private static readonly long MaximumInterval = 60 * 1000; //1 minute in milliseconds
			private readonly TimeSpan defaultInterval;
			private const int PercentageThreshold = 25;
			private DateTimeOffset? start;

			public RetryBackoffCalculator(long defaultInterval) {
				this.defaultInterval = TimeSpan.FromMilliseconds(defaultInterval);
			}

			public RetryException RetryNext(MonitorStatus status, float percentageComplete) {
				if (!start.HasValue || status != MonitorStatus.inProgress || !(percentageComplete > PercentageThreshold)) {
					if (!start.HasValue) {
						start = DateTimeOffset.UtcNow;
					}
					return new RetryMonitorException(defaultInterval);
				}

				var current = DateTimeOffset.UtcNow;
				var elapsed = (current - start.Value).TotalMilliseconds;
				var total = (int)elapsed * 100 / percentageComplete;
				return new RetryMonitorException(TimeSpan.FromMilliseconds(Math.Min(total - elapsed, MaximumInterval)));
			}
		}

		private enum MonitorStatus
		{
			// ReSharper disable UnusedMember.Local
			// ReSharper disable InconsistentNaming
			notStarted,
			inProgress,
			completed,
			updating,
			failed,
			deletePending,
			deleteFailed,
			waiting
		}
	}
}