using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PortalArchitects.Connectors.Management.Models;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Get, "SkySyncItemAcl")]
	public class GetItemAcl : ConnectionCmdlet
	{
		[Parameter(Position = LastParameterPosition + 1, Mandatory = false)]
		public string Path {
			get;
			set;
		}

		protected override void ProcessRecord() {
			List<ManagedAcl> items = null;
			WithClient(async (client, token) => {
				var result = await client.GetPermissions(Path, true, token);
				items = result.Rules.Select(x => new ManagedAcl(x)).ToList();
			});

			if (items != null) {
				WriteObject(items, true);
			}
		}
	}
}