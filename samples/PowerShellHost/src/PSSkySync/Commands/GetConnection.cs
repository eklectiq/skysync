using System.Management.Automation;
using PortalArchitects.Connectors.Management.Models;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Get, "SkySyncConnection")]
	public class GetConnection : ConnectionCmdlet
	{
		protected override void ProcessRecord() {
			ManagedConnection connection = null;

			WithClient((client, token) => {
				connection = new ManagedConnection(client.Connection);
				return CompletedTask;
			});

			if (connection != null) {
				WriteObject(connection);
			}
		}
	}
}