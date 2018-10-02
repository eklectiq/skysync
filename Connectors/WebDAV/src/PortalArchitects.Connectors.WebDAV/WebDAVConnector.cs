using System;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.WebDAV.Pipeline;
using PortalArchitects.Connectors.Security;
using PortalArchitects.Connectors.Spi;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Runtime.Pipeline;

namespace PortalArchitects.Connectors.WebDAV
{
	public class WebDAVConnector : IConnector, IServiceProvider
	{
		private readonly ConnectionState connectionState;
		private readonly PlatformPathValidation pathValidation;
		private readonly Lazy<IExecutionPipeline> pipeline;

		private WebDAVConnector() {
			AllowedItemTypes = new PlatformSupportedItemTypes {
				DefaultTypes = new PlatformDefaultItemTypes {
					DefaultContainerType = PlatformItemTypes.Directory,
					DefaultItemType = PlatformItemTypes.File
				},
				SupportedTypes = new PlatformItemTypeList {
					PlatformItemTypes.File,
					PlatformItemTypes.Directory
				},
				AllTypes = new PlatformItemTypeList {
					PlatformItemTypes.File,
					PlatformItemTypes.Directory
				}
			};
			pathValidation = WebDAVConnectorProvider.PlatformPathValidation.Copy();
			Features = new ConnectorFeatures(WebDAVConnectorProvider.PlatformFeatures);
			pipeline = new Lazy<IExecutionPipeline>(() => new SimpleExecutionPipeline {
				{typeof(GetInfoByPathOperation), new GetInfoByPathOperationExecutor(connectionState)},
				{typeof(GetChildrenOperation), new GetChildrenOperationExecutor(connectionState)},
				{typeof(CreateContainerOperation), new CreateContainerOperationExecutor(connectionState)},
				{typeof(DeleteItemOperation), new DeleteItemOperationExecutor(connectionState)},
				{typeof(RenameItemOperation), new RenameItemOperationExecutor(connectionState)},
				{typeof(ReadItemOperation), new ReadItemOperationExecutor(connectionState)},
				{typeof(WriteItemOperation), new WriteItemOperationExecutor(connectionState)},
				{typeof(NativeCopyOperation), new NativeTransferOperationExecutor(connectionState)},
				{typeof(NativeMoveOperation), new NativeTransferOperationExecutor(connectionState)}
			});
		}

		internal WebDAVConnector(ConnectionState connectionState)
			: this() {
			this.connectionState = connectionState;
		}

		public string ProviderName => WebDAVConnectorProvider.ProviderName;

		public IPlatformItemLoadStrategy LoadStrategy {
			get;
		} = new PlatformItemLoadStrategy {
			CanLoadByPath = true
		};

		public IConnectorFeatures Features {
			get;
		}

		public IExecutionPipeline Pipeline => pipeline.Value;

		public PlatformSupportedItemTypes AllowedItemTypes {
			get;
		}

		public AccountDefinition Account => connectionState.Account;

		public IPathValidation PathValidation => pathValidation;

		public void Dispose() {
			connectionState.Dispose();
		}

		object IServiceProvider.GetService(Type serviceType) {
			if (serviceType == typeof(ConnectionState)) {
				return connectionState;
			}
			if (serviceType == typeof(System.Net.Http.HttpClient)) {
				return connectionState.HttpClient;
			}
			return null;
		}
	}
}
