using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace DataHunter.Data;

internal class DirectoryRepository
{
	private readonly DataHunterDbContext db;
	private readonly Dictionary<string, ObservableCollection<DirectoryMetadata>> directoryCollections = new();

	public DirectoryRepository(DataHunterDbContext dbContext)
	{
		db = dbContext;
		db.Database.Migrate();
	}

	public void SeedDirectories(IEnumerable<DirectoryMetadata> directories)
	{
		db.DirectoryMetadata.AddRange(directories);
		db.SaveChanges();
	}

	public ObservableCollection<DirectoryMetadata> GetDirectoryCollection(string path)
	{
		if (!directoryCollections.ContainsKey(path))
		{
			
			directoryCollections.Add(path, new(
				from dir in db.DirectoryMetadata.AsNoTracking()
				where dir.Parent == path
				select dir
			));
		}
		return directoryCollections[path];
	}

	public DirectoryMetadata? GetDirectory(string path)
	{
		if (path.EndsWith(":\\"))
		{
			return db.DirectoryMetadata.AsNoTracking().FirstOrDefault(d => d.Drive == path && d.Parent == string.Empty);
		}
		var parent = Path.GetDirectoryName(path);
		var name = Path.GetFileName(path);

		var directory = db.DirectoryMetadata.AsNoTracking().FirstOrDefault(d => d.Parent == parent && d.Name == name);
		return directory;
	}

	public void RemoveDirectory(string path)
	{
		RemoveDirectoryFromCollection(path);
		var directory = GetDirectory(path);
		if (directory == null)
		{
			return;
		}
		db.DirectoryMetadata.Remove(directory);
		db.SaveChanges();
	}

	private void RemoveDirectoryFromCollection(string path)
	{
		var parent = Path.GetDirectoryName(path);
		if (parent == null || !directoryCollections.ContainsKey(parent))
		{
			return;
		}
		var collection = directoryCollections[parent];
		var name = Path.GetFileName(path);
		var removed = collection.FirstOrDefault(d => d.Name == name);
		if (removed != null)
		{
			collection.Remove(removed);
		}
	}

	public void InsertDirectory(DirectoryMetadata directory)
	{
		var parent = (
			directory.Parent == directory.Drive
				? db.DirectoryMetadata.AsNoTracking().FirstOrDefault(d => d.Drive == directory.Drive && d.Name == string.Empty)
				: GetDirectory(directory.Parent)
			) ?? throw new InvalidOperationException("Parent directory not found");
		
		var exists = db.DirectoryMetadata.AsNoTracking().FirstOrDefault(d => d.Parent == directory.Parent && d.Name == directory.Name);
		if (exists != null)
		{
			throw new InvalidOperationException("The directory already exists in the database");
		}
		var siblings = db.DirectoryMetadata.AsNoTracking().Where(d => d.Parent == directory.Parent).ToArray();

		var insertBefore = siblings
			.Select<DirectoryMetadata, (DirectoryMetadata item, int index)?>((item, index) => (item, index))
			.FirstOrDefault(x => string.Compare(x?.item.Name, directory.Name) > 0)?.index ?? -1;

		long left = insertBefore switch
		{
			0 => parent.Left + 1,
			-1 when siblings.Any() => siblings.Last().Right + 1,
			-1 => parent.Left + 1,
			_ => siblings[insertBefore - 1].Left,
		};

		var offset = directory.Right - directory.Left + 1;

		var toAdd = directory with
		{
			Left = left,
			Right = left + offset - 1
		};

		InsertDirectoryIntoCollection(toAdd);

		db.Database.ExecuteSqlRaw($"UPDATE DirectoryMetadata SET Left = Left + {offset} WHERE Drive = '{toAdd.Drive}' AND Left >= {toAdd.Left}");
		db.Database.ExecuteSqlRaw($"UPDATE DirectoryMetadata SET Right = Right + {offset} WHERE Drive = '{toAdd.Drive}' AND Right >= {toAdd.Left}");
		db.ChangeTracker.Clear();
		db.DirectoryMetadata.Add(toAdd);
		db.SaveChanges();
	}

	private void InsertDirectoryIntoCollection(DirectoryMetadata added)
	{
		if (added.Parent == null || !directoryCollections.ContainsKey(added.Parent))
		{
			return;
		}
		var collection = directoryCollections[added.Parent];
		var exists = collection.FirstOrDefault(d => d.Name == added.Name);
		if (exists != null)
		{
			return;
		}
		var insertBefore = collection
			.Select<DirectoryMetadata, (DirectoryMetadata item, int index)?>((item, index) => (item, index))
			.FirstOrDefault(x => string.Compare(x?.item.Name, added.Name) > 0)?.index ?? -1;

		int index = insertBefore switch
		{
			0 => 0,
			-1 when collection.Any() => collection.Count,
			-1 => 0,
			_ => insertBefore - 1
		};
		collection.Insert(index, added);
	}

	public (long files, long subDirectories) GetSubTreeSizeSQL(string path)
	{
		var directory = GetDirectory(path);
		if (directory == null)
		{
			return default;
		}

		var subDirectories = db.Database
			.SqlQuery<long>($"SELECT SUM(FileBytes) AS Value FROM DirectoryMetadata WHERE Left > {directory.Left} AND Right < {directory.Right}")
			.First();

		return (directory.FileBytes, subDirectories);
	}

	public (long files, long subDirectories) GetSubTreeSize(string path)
	{
		var directory = GetDirectory(path);
		if (directory == null)
		{
			return default;
		}

		var subDirectories = (
			from dir in db.DirectoryMetadata.AsNoTracking()
			where dir.Left > directory.Left && dir.Right < directory.Right
			select dir.FileBytes
		).Sum();

		return (directory.FileBytes, subDirectories);
	}

	public IEnumerable<DirectoryMetadata> GetSubTree(DirectoryMetadata directory)
	{
		return from dir in db.DirectoryMetadata.AsNoTracking()
					 where dir.Drive == directory.Drive && dir.Left > directory.Left && dir.Right < directory.Right
					 select dir;
	}

	public IEnumerable<DirectoryMetadata> GetTreeChildren(DirectoryMetadata directory)
	{
		return from dir in db.DirectoryMetadata.AsNoTracking()
					 where dir.Parent == Path.Combine(directory.Parent, directory.Name)
					 select dir;
	}
}