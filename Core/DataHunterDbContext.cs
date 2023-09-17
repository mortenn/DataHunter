using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataHunter.Data;

internal class DirectoryDbContextFactory : IDesignTimeDbContextFactory<DataHunterDbContext>
{
	public DataHunterDbContext CreateDbContext(string[] args)
	{
		return DataHunterDbContext.Instance;
	}
}

internal class DataHunterDbContext : DbContext
{
	public static readonly DataHunterDbContext Instance = new(":memory:");

	private readonly string dataSource;

	public DataHunterDbContext(string dataSource) : base()
	{
		this.dataSource = dataSource;
	}

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseSqlite($"Data Source={dataSource}");
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<DirectoryMetadata>().HasIndex(e => e.Drive).HasDatabaseName("IX_Drive");
		modelBuilder.Entity<DirectoryMetadata>().HasIndex(e => e.Left).HasDatabaseName("IX_Left");
		modelBuilder.Entity<DirectoryMetadata>().HasIndex(e => e.Right).HasDatabaseName("IX_Right");
		modelBuilder.Entity<DirectoryMetadata>().HasIndex(e => e.Parent).HasDatabaseName("IX_Parent");
		modelBuilder.Entity<DirectoryMetadata>().HasIndex(e => e.Name).HasDatabaseName("IX_Name");
	}

	public DbSet<DirectoryMetadata> DirectoryMetadata { get; set; }
}