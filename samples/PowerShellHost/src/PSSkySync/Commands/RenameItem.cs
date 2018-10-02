using System.Management.Automation;
using PortalArchitects.Connectors.Management.Models;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Rename, "SkySyncItem")]
	public class RenameItem : ConnectionCmdlet
	{
		[Parameter(Position = LastParameterPosition + 1, Mandatory = true)]
		public string Path {
			get;
			set;
		}

		[Parameter(Position = LastParameterPosition + 2, Mandatory = true)]
		public string NewName {
			get;
			set;
		}

		protected override void ProcessRecord() {
			PlatformItemDefinition item = null;
			WithClient(async (client, token) => {
				item = await client.RenameItem(IO.Path.Parse(Path), NewName, token);
			});
			if (item != null) {
				WriteObject(new ManagedItem(item));
			}
		}
	}
}