using System.Management.Automation;
using PortalArchitects.Connectors.Management.Models;
using PortalArchitects.IO;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Set, "SkySyncContent")]
	public class SetItemContent : ConnectionCmdlet
	{
		[Parameter(Position = LastParameterPosition + 1, Mandatory = true)]
		public string Path {
			get;
			set;
		}

		[Parameter(Position = LastParameterPosition + 2, Mandatory = true)]
		public string Content {
			get;
			set;
		}

		protected override void ProcessRecord() {
			PlatformItemDefinition item = null;
			WithClient(async (client, token) => {
				item = await client.UploadItem(Path, new StringByteSource(Content), token);
			});
			if (item != null) {
				WriteObject(new ManagedItem(item));
			}
		}
	}
}