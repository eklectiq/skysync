using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PortalArchitects.Connectors.Management.Models;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Get, "SkySyncItem")]
	public class GetItem : ConnectionCmdlet
	{
		[Parameter(Position = LastParameterPosition + 1, Mandatory = false)]
		public string Path {
			get;
			set;
		}

		protected override void ProcessRecord() {
			List<ManagedItem> items = null;
			WithClient(async (client, token) => {
				var result = await client.PageChildren(Path, options => options.PageSize = 10, token);
				items = result.Data.Select(x => new ManagedItem(x)).ToList();
			});

			if (items != null) {
				WriteObject(items, true);
			}
		}
	}
}