using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PortalArchitects.Connectors.Management.Models;
using PortalArchitects.Data;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.Get, "SkySyncAccounts")]
	public class GetAccounts : ConnectionCmdlet
	{
		[Parameter(Mandatory = false)]
		public string Filter {
			get;
			set;
		}

		protected override void ProcessRecord() {
			List<ManagedAccount> items = null;
			WithClient(async (client, token) => {
				var result = await client.PageAccounts(new DataQuery {
					Page = new PageDescription {
						PageSize = 10
					},
					SearchText = Filter
				}, token);
				items = result.Data.Select(x => new ManagedAccount(x)).ToList();
			});

			if (items != null) {
				WriteObject(items, true);
			}
		}
	}
}