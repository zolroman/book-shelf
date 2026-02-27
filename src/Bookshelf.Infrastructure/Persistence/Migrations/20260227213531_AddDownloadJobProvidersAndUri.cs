using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookshelf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadJobProvidersAndUri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "torrent_magnet",
                table: "download_jobs",
                newName: "download_uri");

            migrationBuilder.AddColumn<string>(
                name: "execution_provider",
                table: "download_jobs",
                type: "text",
                nullable: false,
                defaultValue: "qbittorrent");

            migrationBuilder.AddColumn<string>(
                name: "source_provider",
                table: "download_jobs",
                type: "text",
                nullable: false,
                defaultValue: "jackett");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "execution_provider",
                table: "download_jobs");

            migrationBuilder.DropColumn(
                name: "source_provider",
                table: "download_jobs");

            migrationBuilder.RenameColumn(
                name: "download_uri",
                table: "download_jobs",
                newName: "torrent_magnet");
        }
    }
}
