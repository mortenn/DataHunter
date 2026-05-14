using System.Windows.Media;

namespace DataHunter.ViewModel
{
	public class PieSlice
	{
		public PieSlice(string name, long bytes, Brush brush, DataContainer source = null)
		{
			Name = name;
			Bytes = bytes;
			Brush = brush;
			Source = source;
		}

		public string Name { get; }

		public long Bytes { get; }

		public Brush Brush { get; }

		public DataContainer Source { get; }
	}
}
