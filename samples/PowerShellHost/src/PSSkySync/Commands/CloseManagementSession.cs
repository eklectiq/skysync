using System.Management.Automation;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Close, "SkySync")]
	public class CloseManagementSession : PSCmdlet
	{
		protected override void ProcessRecord() {
			SessionState.CloseManagementSession();
		}
	}
}