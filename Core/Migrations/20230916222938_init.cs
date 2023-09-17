using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DirectoryMetadata",
                columns: table => new
                {
                    Drive = table.Column<string>(type: "TEXT", nullable: false),
                    Parent = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Left = table.Column<long>(type: "INTEGER", nullable: false),
                    Right = table.Column<long>(type: "INTEGER", nullable: false),
                    FileBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DirectoryMetadata", x => new { x.Drive, x.Parent, x.Name });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drive",
                table: "DirectoryMetadata",
                column: "Drive");

            migrationBuilder.CreateIndex(
                name: "IX_Left",
                table: "DirectoryMetadata",
                column: "Left");

            migrationBuilder.CreateIndex(
                name: "IX_Name",
                table: "DirectoryMetadata",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Parent",
                table: "DirectoryMetadata",
                column: "Parent");

            migrationBuilder.CreateIndex(
                name: "IX_Right",
                table: "DirectoryMetadata",
                column: "Right");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DirectoryMetadata");
        }
    }
}
