using System.Net;
using System.Net.Http;
using PortalArchitects.Net.Http;

namespace PortalArchitects.Connectors.WebDAV
{
	internal class RateLimitHandler : Net.Http.RateLimitHandler
	{
		public RateLimitHandler(HttpMessageHandler innerHandler)
			: base(HttpStatusCode.Forbidden, innerHandler) {
		}

		protected override bool IsRateLimitResponse(HttpResponseMessage response) {
			return base.IsRateLimitResponse(response) && response.GetFirstHeaderOrDefault("X-WebDAV-Status")?.Contains("restart") == true;
		}
	}
}