using PortalArchitects.Connectors.Security;

namespace PortalArchitects.Connectors.Management.Models
{
	public class ManagedAccount
	{
		private readonly SecurityIdentifierDefinition sid;

		public ManagedAccount(SecurityIdentifierDefinition sid) {
			this.sid = sid;
		}

		public string Name => sid.GetDisplayName() ?? sid.GetName() ?? sid.ID;
	}
}