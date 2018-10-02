using System.Management.Automation;

namespace PortalArchitects.Connectors.Management
{
	internal static class SessionStateExtensions
	{
		private const string SessionKey = "SkySyncSession";

		public static ManagementSession GetManagementSession(this SessionState sessionState) {
			return GetManagementSession(sessionState, false);
		}

		public static ManagementSession GetManagementSession(this SessionState sessionState, bool createIfNull) {
			var session = sessionState.PSVariable.GetValue(SessionKey) as ManagementSession;
			if (session == null && createIfNull) {
				session = new ManagementSession();
				sessionState.PSVariable.Set(SessionKey, session);
			}
			return session;
		}

		public static void SetManagementSession(this SessionState sessionState, ManagementSession session) {
			if (session == null) {
				sessionState.PSVariable.Remove(SessionKey);
			} else {
				sessionState.PSVariable.Set(SessionKey, session);
			}
		}

		public static void CloseManagementSession(this SessionState sessionState) {
			var session = sessionState.GetManagementSession();
			if (session != null) {
				sessionState.PSVariable.Remove(SessionKey);
				session.Dispose();
			}
		}
	}
}