using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PortalArchitects.Connectors.IO;
using PortalArchitects.Connectors.Spi;
using PortalArchitects.Connectors.Transfers;
using PortalArchitects.Net.Authorization;
using PortalArchitects.Net.Http;

namespace PortalArchitects.Connectors.WebDAV
{
	[LicensedConnector]
	public class WebDAVConnectorProvider : StoragePlatform, IConnectorProvider, IPromptServiceFactory<Authentication>
	{
		public const string ProviderName = "webdav";

		private readonly IHttpClientFactory httpClientFactory;

		public WebDAVConnectorProvider(IServiceProvider serviceProvider)
			: this(serviceProvider?.GetService<IHttpClientFactory>()) {
		}

		protected WebDAVConnectorProvider(IHttpClientFactory httpClientFactory) {
			this.httpClientFactory = httpClientFactory;
			ID = ProviderName;
			Name = "WebDAV";
			Features = new ConnectorProviderFeatures(PlatformFeatures);
			Locality = StoragePlatformLocality.Specialty;
			PathValidationRules = PlatformPathValidation;
		}

		public static IConnectorProviderFeatures PlatformFeatures {
			get;
		} = new ConnectorProviderFeatures {
			NativeCopy = new NativeTransferSupport {
				CanOverwrite = true
			},
			NativeMove = new NativeTransferSupport {
				CanOverwrite = true
			}
		};

		public static PlatformPathValidation PlatformPathValidation {
			get;
		} = PlatformPathValidation.New()
			.WithMaxSegmentLength(255)
			.PreventLeadingAndTrailingWhitespace()
			.PreventNonPrintableAsciiCharacters()
			.ExcludeCharacters(" +<>:\"/\\|?*")
			.Build();

		public Task<IConnector> OpenConnection(LogInOperation operation, CancellationToken token) {
			var apiClient = new ConnectionState(httpClientFactory, operation);

			if (operation.ValidateConnection) {
				//TODO: make "test" request to validate connection
			}

			return Task.FromResult<IConnector>(new WebDAVConnector(apiClient));
		}

		IEnumerable<PromptAttribute> IPromptServiceFactory.GetAuthorizePrompt() {
			return ((IPromptServiceFactory<Authentication>)this).GetAuthorizePrompt(null);
		}

		IEnumerable<PromptAttribute> IPromptServiceFactory<Authentication>.GetAuthorizePrompt(Authentication authentication) {
			yield return new PromptAttribute {
				AttributeType = PromptAttributeType.Uri,
				DefaultValue = authentication?.Uri,
				IsRequired = true
			};
			yield return new PromptAttribute {
				AttributeType = PromptAttributeType.UserName,
				DefaultValue = authentication?.UserName,
				IsRequired = true
			};
			yield return new PromptAttribute {
				AttributeType = PromptAttributeType.Password,
				DefaultValue = authentication?.Password,
				IsRequired = true
			};
		}
	}
}
