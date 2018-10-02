using PortalArchitects.Connectors.Scenarios;
using Xunit;

namespace PortalArchitects.Connectors.WebDAV
{
	[Trait("Connector", "WebDAV")]
	public class WebDAVFunctionalTests : IConnectorTestFixture<WebDAVConnectorProvider>
	{
		[ScenarioFact]
		public IConnectorTestScenario sanity_test() {
			return new SanityTestScenario();
		}
	}
}
