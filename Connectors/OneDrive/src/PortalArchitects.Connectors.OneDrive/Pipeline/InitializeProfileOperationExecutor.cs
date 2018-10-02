using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Security;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive.Pipeline
{
	internal class InitializeProfileOperationExecutor : PipelineExecutorBase<InitializeProfileOperation, AccountDefinition>
	{
		private readonly ConnectionState client;

		public InitializeProfileOperationExecutor(ConnectionState client) {
			this.client = client;
		}

		public override async Task<AccountDefinition> ExecuteAsync(InitializeProfileOperation operation, CancellationToken token) {
			var drive = await client.LoadDefaultDrive(token);
			if (drive != null && !drive.HasValues) {
				var quota = drive["quota"];
				if (quota != null) {
					client.Features.StorageQuota = quota.ParseQuota();
				}

				client.Owner = drive["owner"]?["user"]?.ParseAccount() ?? client.Owner;
			}
			return client.Owner;
		}
	}
}