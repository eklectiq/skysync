using System.Management.Automation;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Get, "SkySyncProviders")]
	public class GetConnectorProviders : PSCmdlet
	{
		protected override void ProcessRecord() {
			var session = SessionState.GetManagementSession(true);
			WriteObject(session.GetAvailablePlatforms(), true);
		}
	}
}