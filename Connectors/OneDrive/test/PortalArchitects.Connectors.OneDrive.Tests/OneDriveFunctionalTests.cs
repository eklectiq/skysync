using PortalArchitects.Connectors.Scenarios;
using Xunit;

namespace PortalArchitects.Connectors.OneDrive
{
	[Trait("Connector", "Microsoft")]
	[Trait("Connector", "OneDrive")]
	public class OneDriveFunctionalTests : IConnectorTestFixture<OneDriveConnectorProvider>
	{
		[ScenarioFact]
		public IConnectorTestScenario sanity_test() {
			return new SanityTestScenario();
		}
		
		[ScenarioFact]
		public IConnectorTestScenario chunked_upload_scenario() {
			return new ChunkUploadSupportScenario(50 * 1024 * 1024, true);
		}
	}
}