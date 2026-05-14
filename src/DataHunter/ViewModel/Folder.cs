using System.Collections.Generic;
using System.IO;

namespace DataHunter.ViewModel
{
	public class Folder : DataContainer
	{
		public Folder(string fullName, DataContainer parent, FolderScanCache cache) : base(parent, cache)
		{
			Info = new DirectoryInfo(fullName);
		}

		public DirectoryInfo Info { get; }

		public override string FullName => Info.FullName;

		public override string Name => Info.Name;

		public override List<DataContainer> Path
		{
			get
			{
				var path = Parent.Path;
				path.Add(this);
				return path;
			}
		}

		public override bool IsVirtual => Cache.GetOrCreate(FullName).IsVirtual;

		protected override Folder CreateFolder(FolderScanEntry entry)
		{
			return new Folder(entry.FullName, this, Cache);
		}
	}
}