using System.Management.Automation;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Remove, "SkySyncItem")]
	public class RemoveItem : ConnectionCmdlet
	{
		[Parameter(Position = LastParameterPosition + 1, Mandatory = true)]
		public string Path {
			get;
			set;
		}

		protected override void ProcessRecord() {
			WithClient(async (client, token) => {
				await client.DeleteItem(IO.Path.Parse(Path), token);
			});
		}
	}
}