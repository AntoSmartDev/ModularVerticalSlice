using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModularVerticalSlice.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogEventXminConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "events",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "events");
        }
    }
}
