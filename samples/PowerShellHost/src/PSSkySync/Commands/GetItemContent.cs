using System.IO;
using System.Management.Automation;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Get, "SkySyncContent")]
	public class GetItemContent : ConnectionCmdlet
	{
		[Parameter(Position = LastParameterPosition + 1, Mandatory = false)]
		public string Path {
			get;
			set;
		}

		protected override void ProcessRecord() {
			string data = null;
			WithClient(async(client, token) => {
				using (var source = await client.ReadStream(Path, token))
				using (var reader = new StreamReader(source)) {
					data = await reader.ReadToEndAsync();
				}
			});

			if (data != null) {
				WriteObject(data);
			}
		}
	}
}