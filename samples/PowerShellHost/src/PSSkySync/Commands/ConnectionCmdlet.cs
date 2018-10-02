using System;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace PortalArchitects.Connectors.Management.Commands
{
	public abstract class ConnectionCmdlet : PSCmdlet
	{
		protected const int LastParameterPosition = 1;

		protected static readonly Task CompletedTask = Task.FromResult(0);

		private CancellationTokenSource cancellationTokenSource;

		[Parameter(Position = 0, Mandatory = true)]
		[ValidateNotNullOrEmpty]
		public string ProviderName {
			get;
			set;
		}

		[Parameter(Position = 1, Mandatory = true)]
		[ValidateNotNullOrEmpty]
		public string ConnectionName {
			get;
			set;
		}

		protected sealed override void StopProcessing() {
			cancellationTokenSource?.Cancel();
		}

		protected void WithClient(Func<ConnectorClient, CancellationToken, Task> func) {
			cancellationTokenSource?.Dispose();
			using (cancellationTokenSource = new CancellationTokenSource()) {
				try {
					var token = cancellationTokenSource.Token;
					SessionState.GetManagementSession(true).WithClient(ProviderName, ConnectionName, client => func(client, token), token).GetAwaiter().GetResult();
				} catch (Exception e) when (e.IsCancelledException()) {
					//ignore
				} catch (Exception e) {
					WriteError(new ErrorRecord(e, "", ErrorCategory.ResourceUnavailable, null));
				}
			}
		}
	}
}