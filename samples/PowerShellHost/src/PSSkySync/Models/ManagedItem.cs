using System;
using PortalArchitects.Connectors.IO;

namespace PortalArchitects.Connectors.Management.Models
{
	public class ManagedItem
	{
		private readonly PlatformItemDefinition item;

		public ManagedItem(PlatformItemDefinition item) {
			this.item = item;
		}

		public string Name => item.PlatformName;

		public long? Size => !item.IsContainer.GetValueOrDefault() ? (long?)item.Bytes : null;

		public DateTimeOffset? CreatedOn => (item.ContentCreatedOn ?? item.CreatedOn)?.ToLocalTime();

		public DateTimeOffset? ModifiedOn => (item.ContentModifiedOn ?? item.ModifiedOn)?.ToLocalTime();

		public string Path => item.GetFullName();
	}
}