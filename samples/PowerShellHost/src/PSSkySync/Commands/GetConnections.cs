using System.Management.Automation;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Get, "SkySyncConnections")]
	public class GetConnections : PSCmdlet
	{
		protected override void ProcessRecord() {
			var session = SessionState.GetManagementSession(true);
			WriteObject(session.GetConnections(), true);
		}
	}
}