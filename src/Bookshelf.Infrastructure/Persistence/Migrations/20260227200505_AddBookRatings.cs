using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookshelf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "book_ratings",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    book_id = table.Column<long>(type: "bigint", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_ratings", x => new { x.user_id, x.book_id });
                    table.CheckConstraint("ck_book_ratings_rating", "rating >= 1 AND rating <= 5");
                    table.ForeignKey(
                        name: "FK_book_ratings_books_book_id",
                        column: x => x.book_id,
                        principalTable: "books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_book_ratings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_book_ratings_book_id",
                table: "book_ratings",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_ratings_user_id",
                table: "book_ratings",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "book_ratings");
        }
    }
}
