using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using PortalArchitects.Connectors.Authorization;
using PortalArchitects.Connectors.Data;
using PortalArchitects.Connectors.Management.Models;
using PortalArchitects.Connectors.Pipeline;
using PortalArchitects.Connectors.Spi;
using PortalArchitects.DataProtection;
using PortalArchitects.Runtime.Serialization;

// ReSharper disable ClassNeverInstantiated.Local

namespace PortalArchitects.Connectors.Management
{
	public class ManagementSession : IDisposable
	{
		private IServiceProvider serviceProvider;

		static ManagementSession() {
			JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
				Converters = {
					new UnixTimeConverter()
				}
			};
		}

		public ManagementSession()
			: this(null) {
		}

		public ManagementSession(string connectionsPath) {
			var entryPointAssembly = typeof(ManagementSession).GetTypeInfo().Assembly;
			// ReSharper disable once AssignNullToNotNullAttribute
			var searchPath = new DirectoryInfo(Path.GetDirectoryName(entryPointAssembly.Location));
			foreach (var dllFile in searchPath.GetFiles("*.dll")) {
				try {
					//work-around for how powershell loads assemblies in cmdlet
					Assembly.LoadFile(dllFile.FullName);
				} catch (Exception) {
					//ignore
				}
			}

			serviceProvider = new ServiceCollection()
				.AddConnectionHosting(new ConnectorDiscoveryOptions {
					RequireLicensedProviders = false,
					DirectoryPaths = new List<DirectoryInfo> {
						searchPath
					}
				})
				.AddConnectionByIDStore<FileConnectionByIDStore>()
				.AddConnectionUpdaterFactory<FileConnectionUpdateFactory>()
				.AddSingleton(new FileConnectionRepository(connectionsPath))
				.AddSingleton<IDataProtector, NoOpDataProtector>()
				.BuildServiceProvider();
		}

		internal AuthorizeRequestFactory GetAuthorizeRequestFactory() {
			return new AuthorizeRequestFactory(serviceProvider.GetRequiredService<IDataProtector>());
		}

		internal IEnumerable<ManagedConnection> GetConnections() {
			return serviceProvider.GetRequiredService<FileConnectionRepository>().GetConnections().Select(x => new ManagedConnection(x));
		}

		internal IConnectorProvider GetConnectorProvider(string id) {
			return serviceProvider.GetRequiredService<IConnectorProviderRegistry>()[id];
		}

		internal IEnumerable<ManagedPlatform> GetAvailablePlatforms() {
			var registry = serviceProvider.GetRequiredService<IConnectorProviderRegistry>();
			return registry.Select(x => new ManagedPlatform(x)).ToList();
		}

		internal async Task WithClient(string provider, string connection, Func<ConnectorClient, Task> func, CancellationToken token) {
			var contextFactory = serviceProvider.GetRequiredService<IConnectionExecutionContextFactory>();
			using (var client = await ConnectorClient.OpenAsync(contextFactory, new LogInOperation {
				Connection = new Connection {
					ProviderName = provider,
					ConnectionID = FileConnectionRepository.GetAuthenticationID(provider, connection)
				}
			}, token)) {
				await func(client);
			}
		}

		internal void UpdateConnection(string providerName, string connectionName, Authentication authentication) {
			serviceProvider.GetRequiredService<FileConnectionRepository>().UpdateConnectionInfo(providerName, connectionName, authentication);
		}

		public void Dispose() {
			(serviceProvider as IDisposable)?.Dispose();
			serviceProvider = null;
		}

		private class FileConnectionByIDStore : IConnectionByIDStore
		{
			private readonly FileConnectionRepository repository;

			public FileConnectionByIDStore(FileConnectionRepository repository) {
				this.repository = repository;
			}

			Task<bool> IConnectionByIDStore.IsActiveAsync(Connection item, CancellationToken token) {
				return Task.FromResult(true);
			}

			public Task<Connection> GetByID(string id, CancellationToken token) {
				var connection = repository.GetConnection(id);
				if (connection == null) {
					throw new ConnectionNotFoundException(id);
				}

				return Task.FromResult(connection);
			}
		}

		private class FileConnectionUpdateFactory : IConnectionUpdaterFactory
		{
			private readonly FileConnectionRepository repository;

			public FileConnectionUpdateFactory(FileConnectionRepository repository) {
				this.repository = repository;
			}

			public Task<IConnectionUpdater> BeginUpdate(string id, CancellationToken token) {
				return repository.BeginUpdate(id, token);
			}
		}
	}
}
