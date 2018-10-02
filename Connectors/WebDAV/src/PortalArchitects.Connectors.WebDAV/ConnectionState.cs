using System;
using System.Net;
using System.Net.Http;
using PortalArchitects.Connectors.Security;
using PortalArchitects.Net.Authorization;
using PortalArchitects.Net.Http;

namespace PortalArchitects.Connectors.WebDAV
{
	internal class ConnectionState : IDisposable
	{
		public ConnectionState(IHttpClientFactory httpClientFactory, LogInOperation operation) {
			var authentication = operation.Authentication;
			HttpClient = httpClientFactory.NewHttpClient(WebDAVConnectorProvider.ProviderName, authentication.ID, handler => {
				handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
				return
					new RateLimitHandler(
						new QuotedCharSetMessageHandler(
							new AuthorizationMessageHandler(EncodedAuthenticationTokenHandler.Basic(authentication.UserName, authentication.Password), handler)));
			});
			HttpClient.BaseAddress = operation.EnsureQualifiedBaseAddress();
			Account = new AccountDefinition {
				FullName = authentication.UserName
			};
		}

		public HttpClient HttpClient {
			get;
		}

		public AccountDefinition Account {
			get;
		}

		public void Dispose() {
			HttpClient.Dispose();
		}
	}
}
