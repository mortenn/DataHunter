using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using DataHunter.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Test;

public class UnitTest1
{
	private readonly ITestOutputHelper _output;

	public UnitTest1(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void Test1()
	{
		var dbFile = Path.GetTempFileName();
		File.Delete(dbFile);
		var dbContext = new DataHunterDbContext(dbFile);
		var directories = new DirectoryRepository(dbContext);
		var drive = DriveInfo.GetDrives().First(d => d.DriveType == DriveType.Fixed);
		var walker = new DirectoryWalker(directories, drive);
		walker.Initialize();
		var (files, subDirs) = directories.GetSubTreeSize(drive.RootDirectory.GetDirectories().First().FullName);
		subDirs.Should().BePositive();
		File.Delete(dbFile);
	}

	[Fact]
	public void Test2()
	{
		//var dbFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
		var dbContext = new DataHunterDbContext(":memory:");
		var transaction = dbContext.Database.BeginTransaction();
		dbContext.Database.Migrate();
		transaction.Commit();
		var directories = new DirectoryRepository(dbContext);
		directories.SeedDirectories(new DirectoryMetadata[]{ new("C:\\", "", "", 0, 1, 0) });
		directories.InsertDirectory(new("C:\\", "C:\\", "Folder 1", 0, 1, 42));
		directories.InsertDirectory(new("C:\\", "C:\\", "Folder 2", 0, 1, 69));
		directories.InsertDirectory(new("C:\\", "C:\\Folder 1", "Folder 3", 0, 1, 420));
		
		var metadata = dbContext.DirectoryMetadata.OrderBy(d => d.Left)
			.ToArray().Select(d => (d.Name, d.Left, d.Right));

		metadata.Should().Equal(
			("", 0, 7),
			("Folder 1", 1, 4),
			("Folder 3", 2, 3),
			("Folder 2", 5, 6)
		);
	}

	[Fact]
	public void PerformanceTest()
	{
		var logger = new AccumulationLogger();
		var config = ManualConfig
			.Create(DefaultConfig.Instance)
			.AddLogger(logger)
			.WithOptions(ConfigOptions.DisableOptimizationsValidator);

		BenchmarkRunner.Run<Benchmarks>(config);

		// write benchmark summary
		_output.WriteLine(logger.GetLog());
	}
}

[MemoryDiagnoser]
public class Benchmarks
{
	private readonly DirectoryRepository repository;

	public Benchmarks()
	{
		repository = new DirectoryRepository(new DataHunterDbContext(Path.Combine(Path.GetTempPath(), "datahunter.db")));
		//var drive = DriveInfo.GetDrives().First(d => d.DriveType == DriveType.Fixed);
		//var walker = new DirectoryWalker(repository, drive);
		//walker.Initialize();
	}

	[Benchmark]
	public void QueryFolderSize()
	{
		_ = repository.GetSubTreeSize("C:\\Windows");
	}

	[Benchmark]
	public void QueryFolderSizeSQL()
	{
		_ = repository.GetSubTreeSizeSQL("C:\\Windows");
	}

	[Benchmark]
	public void QueryFolderData()
	{
		_ = repository.GetDirectory("C:\\Windows");
	}
}