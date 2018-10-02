using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.OneDrive.Pipeline;
using PortalArchitects.Connectors.Pipeline;
using PortalArchitects.Connectors.Security;
using PortalArchitects.Connectors.Spi;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Net.Authorization;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.OneDrive
{
	public class OneDriveConnector : IConnector, ISupportReauthenticate, IServiceProvider
	{
		private readonly Lazy<IExecutionPipeline> pipeline;

		internal OneDriveConnector(ConnectionState connectionState, IConnectionExecutionContextAccessor contextAccessor) {
			pipeline = new Lazy<IExecutionPipeline>(() => new SimpleExecutionPipeline {
				{ typeof(InitializeProfileOperation), new InitializeProfileOperationExecutor(connectionState) },
				{ typeof(GetInfoByIDOperation), new GetInfoByIDOperationExecutor(connectionState) },
				{ typeof(GetInfoByPathOperation), new GetInfoByPathOperationExecutor(connectionState) },
				{ typeof(PageChildrenOperation), new PageChildrenOperationExecutor(connectionState) },
				{ typeof(CreateContainerOperation), new CreateContainerOperationExecutor(connectionState) },
				{ typeof(DeleteItemOperation), new DeleteItemOperationExecutor(connectionState) },
				{ typeof(RenameItemOperation), new RenameItemOperationExecutor(connectionState) },
				{ typeof(ReadItemOperation), new ReadItemOperationExecutor(connectionState)},
				{ typeof(WriteItemOperation), new WriteItemOperationExecutor(connectionState, contextAccessor) },
				// NOTE Disable for now (https://skysync.atlassian.net/browse/SS-1644)
				//{ typeof(NativeCopyOperation), new NativeCopyOperationExecutor(connectionState) },
				{ typeof(NativeMoveOperation), new NativeMoveOperationExecutor(connectionState) }
			});
			Client = connectionState;
		}

		internal ConnectionState Client {
			get;
		}

		public string ProviderName => OneDriveConnectorProvider.ProviderName;

		public IPlatformItemLoadStrategy LoadStrategy {
			get;
		} = new PlatformItemLoadStrategy {
			CanLoadByID = true,
			CanLoadByPath = true,
			CanLoadByType = true,
			CanLoadParentEfficiently = true,
			CanSortItems = true,
			CanPageItemsByToken = true
		};

		public IConnectorFeatures Features => Client.Features;

		public IExecutionPipeline Pipeline => pipeline.Value;

		public PlatformSupportedItemTypes AllowedItemTypes => Client.AllowedItemTypes;

		public AccountDefinition Account => Client.Owner;

		public IPathValidation PathValidation => Client.PathValidation;

		public void Dispose() {
			Client.Dispose();
		}

		Task ISupportReauthenticate.UpdateToken(CancellationToken token) {
			return Client.UpdateToken(token);
		}

		object IServiceProvider.GetService(Type serviceType) {
			if (serviceType == typeof(ConnectionState)) {
				return Client;
			}
			if (serviceType == typeof(HttpClient)) {
				return Client.HttpClient;
			}
			return null;
		}
	}
}