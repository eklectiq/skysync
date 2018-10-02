using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.Authorization;
using PortalArchitects.Threading;

namespace PortalArchitects.Connectors.Management
{
	internal class FileConnectionRepository
	{
		private readonly string path;

		public FileConnectionRepository() {
		}

		public FileConnectionRepository(string path) {
			if (string.IsNullOrEmpty(path)) {
				path = Path.Combine(Path.GetTempPath(), "SkySyncConnections.json");
			}
			this.path = path;
		}

		public IEnumerable<string> GetConnectionProviderNames() {
			var configuration = GetConfiguration(path);
			return configuration.Properties().Select(x => x.Name);
		}

		public IEnumerable<string> GetConnectionNames(string providerName) {
			var providerElement = GetConnectionProviderElement(providerName);
			return providerElement?.Properties().Select(x => x.Name) ?? Enumerable.Empty<string>();
		}

		private JObject GetConnectionProviderElement(string providerName) {
			var configuration = GetConfiguration(path);
			return configuration.Value<JObject>(providerName);
		}

		public IEnumerable<Connection> GetConnections() {
			var configuration = GetConfiguration(path);
			return configuration.Properties().SelectMany(provider =>
				provider.Value.Value<JObject>().Properties()
					.Select(connection => {
							var element = connection.Value.Value<JObject>();
							return GetConnection(provider.Name, connection.Name, element);
						})).ToList();
		}

		public Connection GetConnection(string id) {
			string providerName, connection;
			GetProviderNameAndID(id, out providerName, out connection);

			var element = GetConnectionElement(providerName, connection);
			return GetConnection(providerName, connection, element);
		}

		private Connection GetConnection(string providerName, string connection, JObject element) {
			var auth = GetConnectionInfo(providerName, connection, element);
			return auth != null ? new Connection {
				ConnectionID = auth.ID,
				Name = connection,
				ProviderName = providerName,
				Authentication = auth
			} : null;
		}

		public Authentication GetConnectionInfo(string providerName, string connectionName) {
			var element = GetConnectionElement(providerName, connectionName);
			return GetConnectionInfo(providerName, connectionName, element);
		}

		public Authentication GetConnectionInfo(string providerName, string connectionName, JObject element) {
			var auth = element.ToObject<Authentication>();
			auth.ID = GetAuthenticationID(providerName, connectionName);
			return auth;
		}

		private JObject GetConnectionElement(string providerName, string connectionName) {
			var providerElement = GetConnectionProviderElement(providerName);
			if (providerElement == null) {
				throw new NotSupportedException("Provider element was not found: " + providerName);
			}

			return GetConnectionElement(providerElement, connectionName);
		}

		private static JObject GetConnectionElement(JToken providerElement, string connectionName) {
			var connection = providerElement.Value<JObject>(connectionName);
			if (connection == null) {
				throw new ConnectionNotFoundException(connectionName);
			}

			return connection;
		}

		public void UpdateConnectionInfo(string providerName, string connectionId, Authentication authentication) {
			if (string.Equals(providerName, "FS", StringComparison.OrdinalIgnoreCase)) {
				return;
			}

			var obj = JObject.FromObject(authentication);
			lock (typeof(FileConnectionRepository)) {
				SetConfiguration(providerName, connectionId, obj, path);
			}
		}

		public void UpdateConnectionInfo(Authentication authentication) {
			string providerName, connectionId;
			GetProviderNameAndID(authentication.ID, out providerName, out connectionId);

			UpdateConnectionInfo(providerName, connectionId, authentication);
		}

		private static JObject GetConfiguration(string configurationFile) {
			try {
				using (var reader = new JsonTextReader(new StreamReader(new FileInfo(configurationFile).OpenRead()))) {
					return JObject.Load(reader) ?? new JObject();
				}
			} catch (Exception) {
				return new JObject();
			}
		}

		private static void SetConfiguration(string providerName, string id, JObject authentication, string configurationFile) {
			authentication.Remove("id");

			var config = GetConfiguration(configurationFile);
			var provider = config.Value<JObject>(providerName);
			var existing = provider?.Property(id);
			if (existing == null) {
				if (provider == null) {
					provider = new JObject();
					config[providerName] = provider;
				}

				provider[id] = authentication;
			} else {
				existing.Value = authentication;
			}

			File.WriteAllText(configurationFile, config.ToString(Formatting.Indented));
		}

		internal static string GetAuthenticationID(string providerName, string connectionName) {
			return providerName + "_" + connectionName;
		}

		private static void GetProviderNameAndID(string authID, out string providerName, out string connectionName) {
			var parts = authID.Split('_');
			providerName = parts[0];
			connectionName = parts[1];
		}

		public Task<IConnectionUpdater> BeginUpdate(string id, CancellationToken token) {
			return ConfigurationFileConnectionUpdater.CreateUpdater(this, id, token);
		}

		private class ConfigurationFileConnectionUpdater : IConnectionUpdater
		{
			private static readonly AsyncLock updateLock = new AsyncLock();

			private readonly FileConnectionRepository repository;
			private readonly IDisposable @lock;

			private ConfigurationFileConnectionUpdater(FileConnectionRepository repository, Authentication authentication, IDisposable @lock) {
				Authentication = authentication;
				this.repository = repository;
				this.@lock = @lock;
			}

			internal static async Task<IConnectionUpdater> CreateUpdater(FileConnectionRepository repository, string id, CancellationToken token) {
				string providerName, connectionName;
				GetProviderNameAndID(id, out providerName, out connectionName);

				var @lock = await updateLock.LockAsync(token);
				var authentication = repository.GetConnectionInfo(providerName, connectionName);
				return new ConfigurationFileConnectionUpdater(repository, authentication, @lock);
			}

			public void Dispose() {
				@lock.Dispose();
			}

			public Authentication Authentication {
				get;
			}

			public Task Update(CancellationToken token) {
				repository.UpdateConnectionInfo(Authentication);
				return Task.FromResult(0);
			}

			public void AlreadyUpdated() {
			}

			public void Revert() {
			}
		}
	}
}
