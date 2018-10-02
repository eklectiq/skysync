using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PortalArchitects.Net.Http;

namespace PortalArchitects.Connectors.OneDrive
{
	internal static partial class PlatformApi
	{
		internal static bool TryParseException(this JToken error, HttpStatusCode statusCode, out Exception exception) {
			if (error == null || !error.HasValues) {
				exception = null;
				return false;
			}

			var code = error.Value<string>("code");
			if (string.IsNullOrEmpty(code)) {
				exception = null;
				return false;
			}

			var message = error.Value<string>("message");

			Exception innerException;
			exception = error["innererror"].TryParseException(statusCode, out innerException)
				? new ConnectorProviderException(statusCode, code, message, innerException)
				: new ConnectorProviderException(statusCode, code, message);
			return true;
		}

		internal static Task EnsureResponseSuccess(this HttpResponseMessage response) {
			return response.EnsureResponseSuccess(obj => {
				Exception exception = null;
				return obj["error"].TryParseException(response.StatusCode, out exception) ? exception : null;
			});
		}

		internal static async Task EnsureValidPlatformID(this HttpResponseMessage response, Func<Exception> createException) {
			if (response.StatusCode == HttpStatusCode.BadRequest) {
				Exception exception;
				var obj = await response.GetJObjectAsync();
				if (obj["error"].TryParseException(response.StatusCode, out exception)) {
					if (exception.Message.Contains("invalidResourceId")) {
						exception = createException();
					}
					throw new HttpStatusException(response.StatusCode, exception.Message, exception);
				}
				response.EnsureSuccessStatusCode();
			}
		}
	}
}