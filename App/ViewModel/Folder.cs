using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataHunter.ViewModel
{
	internal sealed class Folder : DataContainer
	{
		public Folder(DirectoryInfo dir, DataContainer parent) : base(parent)
		{
			Info = dir;
		}

		public DirectoryInfo Info { get; }

		public override string FullName => Info.FullName;

		public override List<DataContainer> Path
		{
			get
			{
				var path = Parent.Path;
				path.Add(this);
				return path;
			}
		}

		public override bool IsVirtual => Info.Attributes.HasFlag(FileAttributes.ReparsePoint);

		protected override List<Folder> Scan()
		{
			return Info.GetDirectories().Select(d => new Folder(d, this)).ToList();
		}

		protected override long FindBytes()
		{
			if(AccessDenied)
				return 0;
			try
			{
				return Info.GetFiles().Sum(f => f.Length);
			}
			catch(DirectoryNotFoundException)
			{
				AccessDenied = true;
				return 0;
			}
			catch(UnauthorizedAccessException)
			{
				AccessDenied = true;
				return 0;
			}
		}
	}
}