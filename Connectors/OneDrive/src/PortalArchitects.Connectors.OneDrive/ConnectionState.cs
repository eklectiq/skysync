using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Configuration;
using PortalArchitects.Connectors.Authorization;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.Pipeline;
using PortalArchitects.Connectors.Security;
using PortalArchitects.Connectors.Spi;
using PortalArchitects.Net.Authorization;
using PortalArchitects.Net.Http;

namespace PortalArchitects.Connectors.OneDrive
{
	internal class ConnectionState : IDisposable, ISupportReauthenticate
	{
		private const int DefaultMaxRetries = 30;
		private const int DefaultBaseInterval = 5000;

		private readonly IServiceProvider serviceProvider;
		private readonly Authentication authentication;
		private readonly IAuthorizationTokenHandler tokenHandler;
		private readonly Lazy<int> retryCount;
		private readonly Lazy<int> retryInterval;

		internal ConnectionState(IConnectorProviderFeatures providerFeatures, PlatformPathValidation pathValidation, IConnectionOperation operation, IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider, string loginUri, string serviceUri, IApplicationApiKey defaultApiKey) {
			if (providerFeatures == null) {
				throw new ArgumentNullException(nameof(providerFeatures));
			}
			if (pathValidation == null) {
				throw new ArgumentNullException(nameof(pathValidation));
			}
			if (operation == null) {
				throw new ArgumentNullException(nameof(operation));
			}
			if (httpClientFactory == null) {
				throw new ArgumentNullException(nameof(httpClientFactory));
			}

			Features = new ConnectorFeatures(providerFeatures) {
				IsAllowDelete = true,
				IsAllowRename = true
			};

			PathValidation = pathValidation.Copy();
			this.serviceProvider = serviceProvider;
			authentication = operation.Connection.Authentication;
			Owner = operation.Connection.Account;

			IAuthorizationTokenHandler localHandler = null;
			HttpClient = httpClientFactory.NewHttpClient(OneDriveConnectorProvider.ProviderName, authentication.ID, handler => {
				authentication.InitializeApiKey(serviceProvider, OneDriveConnectorProvider.ProviderName, defaultApiKey);
				var tokenUpdater = serviceProvider.CreateTokenUpdater(handler, authentication, () => new OAuth2TokenUpdater(loginUri, authentication, handler));
				localHandler = new AccessTokenHandler<IOAuth2Token>(tokenUpdater, authentication, OAuth2Token.AuthorizationScheme);
				return new RateLimitHandler(HttpStatusCode.ServiceUnavailable, new AuthorizationMessageHandler(localHandler, handler));
			});
			tokenHandler = localHandler;

			HttpClientNoRedirect = httpClientFactory.NewHttpClient(OneDriveConnectorProvider.ProviderName, authentication.ID, handler => {
				handler.AllowAutoRedirect = false;
				return new RateLimitHandler(HttpStatusCode.ServiceUnavailable, new AuthorizationMessageHandler(localHandler, handler));
			});
			HttpClientNoRedirect.BaseAddress = HttpClient.BaseAddress = new Uri(serviceUri, UriKind.Absolute);

			retryCount = new Lazy<int>(() => GetConfigurationValue("retry_count", DefaultMaxRetries));
			retryInterval = new Lazy<int>(() => GetConfigurationValue("retry_interval", DefaultBaseInterval));
		}

		private int GetConfigurationValue(string key, int defaultValue) {
			if (authentication.ContainsExtensionData(key)) {
				return authentication.GetExtensionData<int>(key);
			}
			var configuration = serviceProvider?.GetService<IConfigurationAccessor>();
			return int.TryParse(configuration?[$"{OneDriveConnectorProvider.ProviderName}:{key}"], out var value) ? value : defaultValue;
		}

		public async Task Initialize(LogInOperation operation, CancellationToken token) {
			if (operation.ValidateConnection) {
				await UpdateToken(token);
			}
		}

		public void Dispose() {
			HttpClient.Dispose();
		}

		public HttpClient HttpClient {
			get;
		}

		public HttpClient HttpClientNoRedirect {
			get;
		}

		public ConnectorFeatures Features {
			get;
		}

		public PlatformPathValidation PathValidation {
			get;
			set;
		}

		public int RetryCount => retryCount.Value;

		public int RetryInterval => retryInterval.Value;

		public bool SupportMultiPartUpload {
			get;
			set;
		}

		public PlatformSupportedItemTypes AllowedItemTypes {
			get;
		} = new PlatformSupportedItemTypes {
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

		public AccountDefinition Owner {
			get;
			internal set;
		}

		public Task UpdateToken(CancellationToken token) {
			if (tokenHandler != null) {
				return tokenHandler.UpdateAsync(token);
			}
			return Task.FromResult(0);
		}
	}
}
