using System.Globalization;
using System.IO;
using System.Threading;
using DataHunter.View;
using DataHunter.ViewModel;

namespace DataHunter.Tests;

public class ScanAndFormattingTests
{
	[Fact]
	public async Task ScanAsyncSharesConcurrentScanForSamePath()
	{
		using var temp = new TempDirectory();
		File.WriteAllBytes(Path.Combine(temp.Path, "root.bin"), new byte[128]);
		Directory.CreateDirectory(Path.Combine(temp.Path, "child"));
		File.WriteAllBytes(Path.Combine(temp.Path, "child", "nested.bin"), new byte[256]);

		var cache = new FolderScanCache();
		var rootStarts = 0;
		cache.FolderScanStarted += (_, scan) =>
		{
			if (string.Equals(scan.FullName, temp.Path, StringComparison.OrdinalIgnoreCase))
				Interlocked.Increment(ref rootStarts);
		};

		var first = cache.ScanAsync(temp.Path);
		var second = cache.ScanAsync(temp.Path);

		var results = await Task.WhenAll(first, second);

		Assert.Equal(results[0], results[1]);
		Assert.Equal(384, results[0]);
		Assert.Equal(1, Volatile.Read(ref rootStarts));
	}

	[Fact]
	public void FolderPathReturnsIndependentLists()
	{
		using var temp = new TempDirectory();
		var cache = new FolderScanCache();
		var drive = new Drive(new DriveInfo(Path.GetPathRoot(temp.Path)!), cache);
		var folder = new Folder(temp.Path, drive, cache);

		var first = folder.Path;
		var second = folder.Path;

		Assert.Equal(2, first.Count);
		Assert.Equal(2, second.Count);
		Assert.Same(folder, first[1]);
		Assert.Same(folder, second[1]);
	}

	[Fact]
	public void SizeFormatterLeavesNullSizesBlank()
	{
		var converter = new FormatKbSizeConverter();

		var result = converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

		Assert.Equal(string.Empty, result);
	}

	private sealed class TempDirectory : IDisposable
	{
		public TempDirectory()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"DataHunter.Tests.{Guid.NewGuid():N}");
			Directory.CreateDirectory(Path);
		}

		public string Path { get; }

		public void Dispose()
		{
			if (Directory.Exists(Path))
				Directory.Delete(Path, true);
		}
	}
}
