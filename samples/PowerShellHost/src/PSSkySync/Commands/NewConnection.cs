using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PortalArchitects.Connectors.Authorization;
using PortalArchitects.Connectors.Spi;
using PortalArchitects.DataProtection;
using PortalArchitects.Net.Authorization;
using PortalArchitects.Runtime.Serialization;

namespace PortalArchitects.Connectors.Management.Commands
{
	[Cmdlet(VerbsCommon.New, "SkySyncConnection")]
	public class NewConnection : PSCmdlet
	{
		[Parameter(Position = 0, Mandatory = true)]
		[ValidateNotNullOrEmpty]
		public string ProviderName {
			get;
			set;
		}

		[Parameter(Mandatory = false)]
		public string ConnectionName {
			get;
			set;
		}

		[Parameter(Mandatory = false)]
		public IDictionary<string, object> Authentication {
			get;
			set;
		}

		protected override void ProcessRecord() {
			var session = SessionState.GetManagementSession(true);
			var provider = session.GetConnectorProvider(ProviderName);
			if (provider == null || StoragePlatform.FromProvider(provider).IsRestrictedPlatform) {
				WriteError(new ErrorRecord(new ConnectionNotFoundException(ProviderName), "", ErrorCategory.ResourceUnavailable, null));
				return;
			}

			var auth = Authentication != null
				? JObject.FromObject(Authentication).ToObject<Authentication>()
				: null;
			var authorize = session.GetAuthorizeRequestFactory().AuthorizeAsync(provider, auth, new CallbackProvider(), CancellationToken.None).GetAwaiter().GetResult();
			if (authorize.Method == AuthorizeMethod.Prompt) {
				auth = GetPromptAuthentication(provider, auth, authorize);
			} else if (auth == null && authorize.AuthorizeUri != null && authorize.Method == AuthorizeMethod.OAuth2 || authorize.Method == AuthorizeMethod.OAuth) {
				auth = GetRedirectAuthentication(provider, authorize);
			}

			if (auth != null) {
				var connectionName = ConnectionName;
				if (string.IsNullOrEmpty(connectionName)) {
					connectionName = "ps";
				}

				session.UpdateConnection(ProviderName, connectionName, auth);
			}
		}

		private Authentication GetPromptAuthentication(IConnectorProvider provider, Authentication auth, AuthorizeRequest authorize) {
			var validator = new ConnectionPromptValidator(provider);
			while (true) {
				if (auth == null) {
					auth = new Authentication();
				}

				foreach (var attribute in authorize.Attributes.Values) {
					ReadPromptAttribute(auth, attribute);
				}

				try {
					auth = validator.Validate(JObject.FromObject(auth).ToObject<Authentication>());
					return auth;
				} catch (InvalidPromptAttributeValueException e) {
					WriteError(new ErrorRecord(e, "", ErrorCategory.InvalidArgument, null));
					auth = null;
				}
			}
		}

		private void ReadPromptAttribute(IExtensibleObject auth, PromptAttribute attribute) {
			if (attribute.Options != null) {
				var choices = new System.Collections.ObjectModel.Collection<System.Management.Automation.Host.ChoiceDescription>();
				foreach (var option in attribute.Options.Values) {
					choices.Add(new System.Management.Automation.Host.ChoiceDescription(option.Name));
				}
				var choice = Host.UI.PromptForChoice(attribute.Name, attribute.Hint, choices, 0);
				auth.AddExtensionData(attribute.Name, attribute.Options[choice].ID);
				return;
			}

			var result = Host.UI.Prompt(attribute.Name, attribute.Hint, new System.Collections.ObjectModel.Collection<System.Management.Automation.Host.FieldDescription> {
				new System.Management.Automation.Host.FieldDescription(attribute.Name) {
					HelpMessage = attribute.Hint
				}
			});
			PSObject value;
			if (result.TryGetValue(attribute.Name, out value)) {
				auth.AddExtensionData(attribute.ID, value.BaseObject);
			}
		}

		private static Authentication GetRedirectAuthentication(IConnectorProvider connectorProvider, AuthorizeRequest authorize) {
			Process.Start(new ProcessStartInfo {
				FileName = authorize.AuthorizeUri.ToString(),
				UseShellExecute = true
			});

			var listener = new HttpListener {
				Prefixes = {
					CallbackProvider.Uri
				}
			};
			listener.Start();

			var context = listener.GetContext();
			try {
				if (authorize.Method == AuthorizeMethod.OAuth2) {
					return new ConnectionOAuth2Authorizer(connectorProvider)
						.Validate(context.Request.QueryString["code"], null, CancellationToken.None)
						.GetAwaiter()
						.GetResult();
				}
				if (authorize.Method == AuthorizeMethod.OAuth) {
					return new ConnectionOAuthAuthorizer(connectorProvider, new NoOpDataProtector())
						.Validate(
							context.Request.QueryString["oauth_token"],
							context.Request.QueryString["oauth_secret"],
							null,
							CancellationToken.None
						)
						.GetAwaiter()
						.GetResult();
				}
			} finally {
				context.Response.Abort();
			}

			return null;
		}

		private class CallbackProvider : IConnectorProviderCallbackProvider
		{
			public const string Uri = "http://localhost:9079/";

			public Task<Func<string>> GetOAuth2CallbackHandler(CancellationToken token) {
				return Task.FromResult<Func<string>>(() => $"{Uri}oauth2");
			}

			public Task<Func<string, string>> GetOAuthCallbackHandler(CancellationToken token) {
				return Task.FromResult<Func<string, string>>(secret => $"{Uri}oauth?secret={secret}");
			}

			public Uri GetPromptAuthorizeUri() {
				return null;
			}
		}
	}
}