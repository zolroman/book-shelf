using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Bookshelf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "authors",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_code = table.Column<string>(type: "text", nullable: false),
                    provider_book_key = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    original_title = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    publish_year = table.Column<int>(type: "integer", nullable: true),
                    language_code = table.Column<string>(type: "text", nullable: true),
                    cover_url = table.Column<string>(type: "text", nullable: true),
                    catalog_state = table.Column<string>(type: "text", nullable: false, defaultValue: "archive"),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_books", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_code = table.Column<string>(type: "text", nullable: false),
                    provider_series_key = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    external_subject = table.Column<string>(type: "text", nullable: true),
                    login = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "book_authors",
                columns: table => new
                {
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    author_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_authors", x => new { x.book_id, x.author_id });
                    table.ForeignKey(
                        name: "FK_book_authors_authors_author_id",
                        column: x => x.author_id,
                        principalTable: "authors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_book_authors_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "book_media_assets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    source_provider = table.Column<string>(type: "text", nullable: false, defaultValue: "jackett"),
                    storage_path = table.Column<string>(type: "text", nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    checksum = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "available"),
                    downloaded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_media_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_book_media_assets_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "series_books",
                columns: table => new
                {
                    series_id = table.Column<long>(type: "bigint", nullable: false),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    series_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_series_books", x => new { x.series_id, x.book_id });
                    table.CheckConstraint("ck_series_books_series_order", "series_order > 0");
                    table.ForeignKey(
                        name: "FK_series_books_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_series_books_series_series_id",
                        column: x => x.series_id,
                        principalTable: "series",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "download_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_job_id = table.Column<string>(type: "text", nullable: true),
                    torrent_magnet = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_not_found_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_download_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_download_jobs_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_download_jobs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "history_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    position_ref = table.Column<string>(type: "text", nullable: true),
                    event_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_history_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_history_events_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_history_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "progress_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    position_ref = table.Column<string>(type: "text", nullable: false),
                    progress_percent = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress_snapshots", x => x.id);
                    table.CheckConstraint("ck_progress_snapshots_progress_percent", "progress_percent >= 0 AND progress_percent <= 100");
                    table.ForeignKey(
                        name: "FK_progress_snapshots_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_progress_snapshots_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shelves",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shelves", x => x.id);
                    table.ForeignKey(
                        name: "FK_shelves_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shelf_books",
                columns: table => new
                {
                    shelf_id = table.Column<long>(type: "bigint", nullable: false),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    added_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shelf_books", x => new { x.shelf_id, x.book_id });
                    table.ForeignKey(
                        name: "FK_shelf_books_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shelf_books_shelves_shelf_id",
                        column: x => x.shelf_id,
                        principalTable: "shelves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authors_name",
                table: "authors",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_book_authors_author_id",
                table: "book_authors",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_book_media_assets_book_id_media_type",
                table: "book_media_assets",
                columns: new[] { "book_id", "media_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_books_original_title",
                table: "books",
                column: "original_title");

            migrationBuilder.CreateIndex(
                name: "IX_books_provider_code_provider_book_key",
                table: "books",
                columns: new[] { "provider_code", "provider_book_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_books_title",
                table: "books",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_download_jobs_book_id",
                table: "download_jobs",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_download_jobs_user_status_time",
                table: "download_jobs",
                columns: new[] { "user_id", "status", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_active_download_per_user_book_type",
                table: "download_jobs",
                columns: new[] { "user_id", "book_id", "media_type" },
                unique: true,
                filter: "status IN ('queued', 'downloading')");

            migrationBuilder.CreateIndex(
                name: "IX_history_events_book_id",
                table: "history_events",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_history_user_time",
                table: "history_events",
                columns: new[] { "user_id", "event_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_progress_snapshots_book_id",
                table: "progress_snapshots",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "IX_progress_snapshots_user_id_book_id_media_type",
                table: "progress_snapshots",
                columns: new[] { "user_id", "book_id", "media_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_progress_user_book",
                table: "progress_snapshots",
                columns: new[] { "user_id", "book_id" });

            migrationBuilder.CreateIndex(
                name: "IX_series_provider_code_provider_series_key",
                table: "series",
                columns: new[] { "provider_code", "provider_series_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_series_books_book_id",
                table: "series_books",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "IX_series_books_series_id_series_order",
                table: "series_books",
                columns: new[] { "series_id", "series_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shelf_books_book_id",
                table: "shelf_books",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "IX_shelves_user_id_name",
                table: "shelves",
                columns: new[] { "user_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_external_subject",
                table: "users",
                column: "external_subject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_login",
                table: "users",
                column: "login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_authors");

            migrationBuilder.DropTable(
                name: "book_media_assets");

            migrationBuilder.DropTable(
                name: "download_jobs");

            migrationBuilder.DropTable(
                name: "history_events");

            migrationBuilder.DropTable(
                name: "progress_snapshots");

            migrationBuilder.DropTable(
                name: "series_books");

            migrationBuilder.DropTable(
                name: "shelf_books");

            migrationBuilder.DropTable(
                name: "authors");

            migrationBuilder.DropTable(
                name: "series");

            migrationBuilder.DropTable(
                name: "books");

            migrationBuilder.DropTable(
                name: "shelves");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
