using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataHunter.ViewModel
{
	public class Drive : DataContainer
	{
		public Drive(DriveInfo driveInfo) : base(null)
		{
			Info = driveInfo;
		}

		public DriveInfo Info { get; }

		public override string FullName => Info.Name;

		public override List<DataContainer> Path => new List<DataContainer> { this };

		protected override List<Folder> Scan()
		{
			return Info.RootDirectory.GetDirectories().Select(d => new Folder(d, this)).ToList();
		}

		protected override long FindBytes()
		{
			return Info.RootDirectory.GetFiles().Sum(f => f.Length);
		}
	}
}