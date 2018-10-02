using System;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.Authorization;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.Pipeline;
using PortalArchitects.Connectors.Spi;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Net.Authorization;
using PortalArchitects.Net.Http;

namespace PortalArchitects.Connectors.OneDrive
{
	[LicensedConnector]
	public class OneDriveConnectorProvider : StoragePlatform, IConnectorProvider, IOAuth2ServiceFactory
	{
		public const string ProviderName = "onedrive";

		internal const string LoginUri = "https://login.live.com/oauth20_token.srf";
		internal const string ServiceUri = "https://api.onedrive.com/v1.0/";

		private const string DefaultClientId = "16c79e1a-1b80-4a7f-8c0f-7f9d0c0d479a";
		private const string DefaultClientSecret = "QNbNQWYLMa3qWijYwNgNTtk";
		internal static readonly ApplicationApiKey DefaultApiKey = new ApplicationApiKey(DefaultClientId, DefaultClientSecret);

		private const double PromptWidth = 375;
		private const double PromptHeight = 675;

		private readonly IServiceProvider serviceProvider;
		private readonly IHttpClientFactory httpClientFactory;

		private OneDriveConnectorProvider(IHttpClientFactory httpClientFactory) {
			if (httpClientFactory == null) {
				throw new ArgumentNullException(nameof(httpClientFactory));
			}
			this.httpClientFactory = httpClientFactory;

			ID = ProviderName;
			Name = "Microsoft OneDrive";
			Features = new ConnectorProviderFeatures(PlatformFeatures);
			Locality = StoragePlatformLocality.Cloud;
			Authorization = new StoragePlatformAuthorization {
				AuthorizePrompt = new AuthorizeRequestPrompt {
					SuggestedHeight = PromptHeight,
					SuggestedWidth = PromptWidth
				}
			};
			PathValidationRules = PlatformPathValidation;
		}

		public OneDriveConnectorProvider(IServiceProvider serviceProvider)
			: this(serviceProvider?.GetService<IHttpClientFactory>()) {
			this.serviceProvider = serviceProvider;
		}

		public static IConnectorProviderFeatures PlatformFeatures {
			get;
		} = new ConnectorProviderFeatures {
			IsAllowTimestampPreservation = true,
			IsProvidesImmediateDetailsAfterWrite = true,
			// NOTE Disable for now (https://skysync.atlassian.net/browse/SS-1644)
			// IsAllowNativeCopy = true,
			NativeMove = new NativeTransferSupport {
				IsTimestampsPreserved = true
			},
			MaximumUploadSize = 1024 * 1024 * 1024 * 2L /* 2GB */,
			ApiKeyRequirements = new CustomApiKeySupport().TypicalOAuth2()
		};

		public static PlatformPathValidation PlatformPathValidation {
			get;
		} = PlatformPathValidation.Default().Build();

		public async Task<IConnector> OpenConnection(LogInOperation operation, CancellationToken token) {
			var connectionState = new ConnectionState(PlatformFeatures, PlatformPathValidation, operation, httpClientFactory, serviceProvider, LoginUri, ServiceUri, DefaultApiKey) {
				SupportMultiPartUpload = true
			};

			try {
				await connectionState.Initialize(operation, token);
			} catch (Exception) {
				connectionState.Dispose();
				throw;
			}

			return new OneDriveConnector(connectionState, serviceProvider?.GetService(typeof(IConnectionExecutionContextAccessor)) as IConnectionExecutionContextAccessor);
		}

		OAuth2AuthorizeRequest IOAuth2ServiceFactory.Authorize(IApplicationApiKey apiKey, string state, CancellationToken token) {
			const string scope = "wl.signin+wl.offline_access+onedrive.readwrite";
			const string authorizeUri = "https://login.live.com/oauth20_authorize.srf?response_type=code&scope=" + scope;

			var defaultApiKey = serviceProvider.GetApiKey(ID) ?? DefaultApiKey;
			apiKey = (apiKey.EnsureClientID(defaultApiKey.ClientID, defaultApiKey.ClientSecret) ?? defaultApiKey)
				.EnsureClientRedirect(defaultApiKey.ClientRedirect, serviceProvider);

			return new OAuth2AuthorizeRequestFactory(authorizeUri) {
				SuggestedWidth = PromptWidth,
				SuggestedHeight = PromptHeight
			}.Authorize(apiKey, state, token);
		}

		Task<IOAuth2Token> IOAuth2ServiceFactory.UpdateAccessToken(IOAuth2Token accessToken, IApplicationApiKey apiKey, CancellationToken token) {
			var defaultApiKey = serviceProvider.GetApiKey(ID) ?? DefaultApiKey;
			apiKey = (apiKey.EnsureClientID(defaultApiKey.ClientID, defaultApiKey.ClientSecret) ?? defaultApiKey)
				.EnsureClientRedirect(defaultApiKey.ClientRedirect, serviceProvider);

			return new OAuth2AccessTokenFactory(LoginUri, httpClientFactory)
				.UpdateAccessToken(accessToken, apiKey, token);
		}
	}
}
