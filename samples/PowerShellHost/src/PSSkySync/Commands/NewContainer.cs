using System.Management.Automation;
using PortalArchitects.Connectors.Management.Models;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.New, "SkySyncContainer")]
	public class NewContainer : ConnectionCmdlet
	{
		[Parameter(Position = LastParameterPosition + 1, Mandatory = true)]
		public string Path {
			get;
			set;
		}

		protected override void ProcessRecord() {
			PlatformItemDefinition newcontainer = null;
			WithClient(async (client, token) => {
				newcontainer = await client.NewContainer(Path, token);
			});
			if (newcontainer != null) {
				WriteObject(new ManagedItem(newcontainer));
			}
		}
	}
}