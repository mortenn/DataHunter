using System.Collections.Generic;
using System.IO;

namespace DataHunter.ViewModel
{
	public class Drive : DataContainer
	{
		public Drive(DriveInfo driveInfo, FolderScanCache cache)
			: base(null, cache)
		{
			Info = driveInfo;
		}

		public DriveInfo Info { get; }

		public override string FullName => Info.Name;

		public override string Name => Info.Name;

		public override List<DataContainer> Path => new List<DataContainer> { this };

		public override bool IsVirtual => false;

		protected override Folder CreateFolder(FolderScanEntry entry)
		{
			return new Folder(entry.FullName, this, Cache);
		}
	}
}
