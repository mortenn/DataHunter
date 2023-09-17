using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace DataHunter.Data;

[PrimaryKey(nameof(Drive), nameof(Parent), nameof(Name))]
internal record DirectoryMetadata(string Drive, string Parent, string Name, long Left, long Right, long FileBytes);
