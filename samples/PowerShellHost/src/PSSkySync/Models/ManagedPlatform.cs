using System.Linq;
using PortalArchitects.Connectors.Authorization;
using PortalArchitects.Connectors.Spi;

namespace PortalArchitects.Connectors.Management.Models
{
	public class ManagedPlatform
	{
		private readonly StoragePlatform provider;

		internal ManagedPlatform(IConnectorProvider provider) {
			this.provider = StoragePlatform.FromProvider(provider);
			AuthorizeMethod = AuthorizeMethods.GetAvailableAuthorizationMethods(provider).FirstOrDefault();
		}

		public string ID => provider.ID;

		public string Name => provider.Name;

		public bool IsEnabled => !provider.IsRestrictedPlatform;

		public AuthorizeMethod AuthorizeMethod {
			get;
		}
	}
}