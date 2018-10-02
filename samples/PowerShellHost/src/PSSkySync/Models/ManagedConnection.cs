namespace PortalArchitects.Connectors.Management.Models
{
	public class ManagedConnection
	{
		private readonly Connection connection;

		internal ManagedConnection(Connection connection) {
			this.connection = connection;
		}

		public string ID => connection.ConnectionID;

		public string Name => connection.Name;

		public string ProviderName => connection.ProviderName;

		public string AccountName => connection.PlatformAccountEmail ?? connection.PlatformAccountName;
	}
}