using System;
using System.IO;

namespace DataHunter.Diagnostics
{
	public static class FlightRecorder
	{
		public static string LogPath { get; } =
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"DataHunter",
				"DataHunter.log"
			);

		public static void Log(string message)
		{
			try
			{
				var directory = Path.GetDirectoryName(LogPath);
				if (!string.IsNullOrEmpty(directory))
					Directory.CreateDirectory(directory);

				File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
			}
			catch { }
		}

		public static void Log(Exception exception)
		{
			Log(exception.ToString());
		}
	}
}
