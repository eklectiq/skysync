using System.Management.Automation;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.New, "SkySync")]
	public class NewManagementSession : PSCmdlet
	{
		[Parameter(Mandatory = false)]
		public string ConnectionsPath {
			get;
			set;
		}

		protected override void ProcessRecord() {
			SessionState.CloseManagementSession();
			SessionState.SetManagementSession(new ManagementSession(ConnectionsPath));
		}
	}
}