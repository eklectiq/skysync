using PortalArchitects.Connectors.Security;
using PortalArchitects.Runtime.Serialization;

namespace PortalArchitects.Connectors.Management.Models
{
	public class ManagedAcl
	{
		private readonly ManagedAccount account;
		private readonly AccessRuleDefinition rule;

		public ManagedAcl(AccessRuleDefinition rule) {
			this.rule = rule;
			account = new ManagedAccount(rule.Identifier);
		}

		public string Name => account.Name;

		public string Access => Enums.ToString(rule.AccessControl);

		public string Rights => Enums.ToString(rule.Rights);
	}
}