using Bookshelf.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Bookshelf.Infrastructure.Tests;

public class SchemaMigrationConstraintTests
{
    [Fact]
    public void Books_HasUniqueProviderKeyConstraint()
    {
        var migrationBuilder = BuildMigration();

        var index = migrationBuilder.Operations
            .OfType<CreateIndexOperation>()
            .SingleOrDefault(x =>
                x.Table == "books" &&
                x.IsUnique &&
                x.Columns.SequenceEqual(new[] { "provider_code", "provider_book_key" }));

        Assert.NotNull(index);
    }

    [Fact]
    public void BookMediaAssets_HasUniqueBookAndMediaTypeConstraint()
    {
        var migrationBuilder = BuildMigration();

        var index = migrationBuilder.Operations
            .OfType<CreateIndexOperation>()
            .SingleOrDefault(x =>
                x.Table == "book_media_assets" &&
                x.IsUnique &&
                x.Columns.SequenceEqual(new[] { "book_id", "media_type" }));

        Assert.NotNull(index);
    }

    [Fact]
    public void DownloadJobs_HasActivePartialUniqueIndex()
    {
        var migrationBuilder = BuildMigration();

        var index = migrationBuilder.Operations
            .OfType<CreateIndexOperation>()
            .SingleOrDefault(x => x.Name == "ux_active_download_per_user_book_type");

        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal("status IN ('queued', 'downloading')", index.Filter);
    }

    [Fact]
    public void ProgressSnapshots_HasPercentCheckConstraint()
    {
        var migrationBuilder = BuildMigration();

        var table = migrationBuilder.Operations
            .OfType<CreateTableOperation>()
            .SingleOrDefault(x => x.Name == "progress_snapshots");

        Assert.NotNull(table);
        var checkConstraint = table!.CheckConstraints
            .SingleOrDefault(x => x.Name == "ck_progress_snapshots_progress_percent");

        Assert.NotNull(checkConstraint);
        Assert.Contains("progress_percent", checkConstraint!.Sql, StringComparison.OrdinalIgnoreCase);
    }

    private static MigrationBuilder BuildMigration()
    {
        var migration = new InitialCreateProxy();
        var migrationBuilder = new MigrationBuilder(activeProvider: "Npgsql.EntityFrameworkCore.PostgreSQL");
        migration.ApplyUp(migrationBuilder);
        return migrationBuilder;
    }

    private sealed class InitialCreateProxy : InitialCreate
    {
        public void ApplyUp(MigrationBuilder builder)
        {
            Up(builder);
        }
    }
}
