using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Bookshelf.Infrastructure.Persistence;

public sealed class BookshelfDbContext : DbContext
{
    private static readonly ValueConverter<MediaType, string> MediaTypeConverter =
        new(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<MediaType>(value, ignoreCase: true));

    private static readonly ValueConverter<CatalogState, string> CatalogStateConverter =
        new(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<CatalogState>(value, ignoreCase: true));

    private static readonly ValueConverter<MediaAssetStatus, string> MediaAssetStatusConverter =
        new(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<MediaAssetStatus>(value, ignoreCase: true));

    private static readonly ValueConverter<HistoryEventType, string> HistoryEventTypeConverter =
        new(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<HistoryEventType>(value, ignoreCase: true));

    private static readonly ValueConverter<DownloadJobStatus, string> DownloadJobStatusConverter =
        new(
            value => value.ToString().ToLowerInvariant(),
            value => Enum.Parse<DownloadJobStatus>(value, ignoreCase: true));

    public BookshelfDbContext(DbContextOptions<BookshelfDbContext> options)
        : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Author> Authors => Set<Author>();

    public DbSet<BookAuthor> BookAuthors => Set<BookAuthor>();

    public DbSet<Series> Series => Set<Series>();

    public DbSet<SeriesBook> SeriesBooks => Set<SeriesBook>();

    public DbSet<BookMediaAsset> BookMediaAssets => Set<BookMediaAsset>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Shelf> Shelves => Set<Shelf>();

    public DbSet<ShelfBook> ShelfBooks => Set<ShelfBook>();

    public DbSet<ProgressSnapshot> ProgressSnapshots => Set<ProgressSnapshot>();

    public DbSet<HistoryEvent> HistoryEvents => Set<HistoryEvent>();

    public DbSet<DownloadJob> DownloadJobs => Set<DownloadJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureBooks(modelBuilder);
        ConfigureAuthors(modelBuilder);
        ConfigureBookAuthors(modelBuilder);
        ConfigureSeries(modelBuilder);
        ConfigureSeriesBooks(modelBuilder);
        ConfigureBookMediaAssets(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureShelves(modelBuilder);
        ConfigureShelfBooks(modelBuilder);
        ConfigureProgressSnapshots(modelBuilder);
        ConfigureHistoryEvents(modelBuilder);
        ConfigureDownloadJobs(modelBuilder);
    }

    private static void ConfigureBooks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Book>();
        entity.ToTable("books");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.ProviderCode).HasColumnName("provider_code").IsRequired();
        entity.Property(x => x.ProviderBookKey).HasColumnName("provider_book_key").IsRequired();
        entity.Property(x => x.Title).HasColumnName("title").IsRequired();
        entity.Property(x => x.OriginalTitle).HasColumnName("original_title");
        entity.Property(x => x.Description).HasColumnName("description");
        entity.Property(x => x.PublishYear).HasColumnName("publish_year");
        entity.Property(x => x.LanguageCode).HasColumnName("language_code");
        entity.Property(x => x.CoverUrl).HasColumnName("cover_url");
        entity.Property(x => x.CatalogState)
            .HasColumnName("catalog_state")
            .HasConversion(CatalogStateConverter)
            .HasDefaultValue(CatalogState.Archive)
            .IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("now()");

        entity.HasIndex(x => new { x.ProviderCode, x.ProviderBookKey }).IsUnique();
        entity.HasIndex(x => x.Title).HasDatabaseName("ix_books_title");
        entity.HasIndex(x => x.OriginalTitle).HasDatabaseName("ix_books_original_title");
    }

    private static void ConfigureAuthors(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Author>();
        entity.ToTable("authors");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.Name).HasColumnName("name").IsRequired();
        entity.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ix_authors_name");
    }

    private static void ConfigureBookAuthors(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BookAuthor>();
        entity.ToTable("book_authors");
        entity.HasKey(x => new { x.BookId, x.AuthorId });
        entity.Property(x => x.BookId).HasColumnName("book_id");
        entity.Property(x => x.AuthorId).HasColumnName("author_id");
        entity.HasOne(x => x.Book)
            .WithMany(x => x.BookAuthors)
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Author)
            .WithMany(x => x.BookAuthors)
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSeries(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Series>();
        entity.ToTable("series");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.ProviderCode).HasColumnName("provider_code").IsRequired();
        entity.Property(x => x.ProviderSeriesKey).HasColumnName("provider_series_key").IsRequired();
        entity.Property(x => x.Title).HasColumnName("title").IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
        entity.HasIndex(x => new { x.ProviderCode, x.ProviderSeriesKey }).IsUnique();
    }

    private static void ConfigureSeriesBooks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SeriesBook>();
        entity.ToTable("series_books");
        entity.HasKey(x => new { x.SeriesId, x.BookId });
        entity.Property(x => x.SeriesId).HasColumnName("series_id");
        entity.Property(x => x.BookId).HasColumnName("book_id");
        entity.Property(x => x.SeriesOrder).HasColumnName("series_order").IsRequired();
        entity.HasOne(x => x.Series)
            .WithMany(x => x.SeriesBooks)
            .HasForeignKey(x => x.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Book)
            .WithMany(x => x.SeriesBooks)
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasIndex(x => new { x.SeriesId, x.SeriesOrder }).IsUnique();
        entity.ToTable(table => table.HasCheckConstraint("ck_series_books_series_order", "series_order > 0"));
    }

    private static void ConfigureBookMediaAssets(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BookMediaAsset>();
        entity.ToTable("book_media_assets");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.BookId).HasColumnName("book_id");
        entity.Property(x => x.MediaType).HasColumnName("media_type").HasConversion(MediaTypeConverter).IsRequired();
        entity.Property(x => x.SourceUrl).HasColumnName("source_url");
        entity.Property(x => x.SourceProvider)
            .HasColumnName("source_provider")
            .IsRequired()
            .HasDefaultValue("jackett");
        entity.Property(x => x.StoragePath).HasColumnName("storage_path");
        entity.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
        entity.Property(x => x.Checksum).HasColumnName("checksum");
        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(MediaAssetStatusConverter)
            .HasDefaultValue(MediaAssetStatus.Available)
            .IsRequired();
        entity.Property(x => x.DownloadedAtUtc).HasColumnName("downloaded_at_utc");
        entity.Property(x => x.DeletedAtUtc).HasColumnName("deleted_at_utc");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("now()");
        entity.HasIndex(x => new { x.BookId, x.MediaType }).IsUnique();
        entity.HasOne(x => x.Book)
            .WithMany(x => x.MediaAssets)
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();
        entity.ToTable("users");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.ExternalSubject).HasColumnName("external_subject");
        entity.Property(x => x.Login).HasColumnName("login").IsRequired();
        entity.Property(x => x.DisplayName).HasColumnName("display_name");
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
        entity.HasIndex(x => x.Login).IsUnique();
        entity.HasIndex(x => x.ExternalSubject).IsUnique();
    }

    private static void ConfigureShelves(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Shelf>();
        entity.ToTable("shelves");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.Property(x => x.Name).HasColumnName("name").IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
        entity.HasIndex(x => new { x.UserId, x.Name }).IsUnique();
        entity.HasOne(x => x.User)
            .WithMany(x => x.Shelves)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureShelfBooks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShelfBook>();
        entity.ToTable("shelf_books");
        entity.HasKey(x => new { x.ShelfId, x.BookId });
        entity.Property(x => x.ShelfId).HasColumnName("shelf_id");
        entity.Property(x => x.BookId).HasColumnName("book_id");
        entity.Property(x => x.AddedAtUtc).HasColumnName("added_at_utc").HasDefaultValueSql("now()");
        entity.HasOne(x => x.Shelf)
            .WithMany(x => x.ShelfBooks)
            .HasForeignKey(x => x.ShelfId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureProgressSnapshots(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProgressSnapshot>();
        entity.ToTable(
            "progress_snapshots",
            table => table.HasCheckConstraint(
                "ck_progress_snapshots_progress_percent",
                "progress_percent >= 0 AND progress_percent <= 100"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.Property(x => x.BookId).HasColumnName("book_id");
        entity.Property(x => x.MediaType).HasColumnName("media_type").HasConversion(MediaTypeConverter).IsRequired();
        entity.Property(x => x.PositionRef).HasColumnName("position_ref").IsRequired();
        entity.Property(x => x.ProgressPercent).HasColumnName("progress_percent").HasColumnType("numeric(5,2)");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("now()");
        entity.HasIndex(x => new { x.UserId, x.BookId, x.MediaType }).IsUnique();
        entity.HasIndex(x => new { x.UserId, x.BookId }).HasDatabaseName("ix_progress_user_book");
        entity.HasOne(x => x.User)
            .WithMany(x => x.ProgressSnapshots)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureHistoryEvents(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HistoryEvent>();
        entity.ToTable("history_events");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.Property(x => x.BookId).HasColumnName("book_id");
        entity.Property(x => x.MediaType).HasColumnName("media_type").HasConversion(MediaTypeConverter).IsRequired();
        entity.Property(x => x.EventType).HasColumnName("event_type").HasConversion(HistoryEventTypeConverter).IsRequired();
        entity.Property(x => x.PositionRef).HasColumnName("position_ref");
        entity.Property(x => x.EventAtUtc).HasColumnName("event_at_utc").HasDefaultValueSql("now()");
        entity.HasIndex(x => new { x.UserId, x.EventAtUtc }).HasDatabaseName("ix_history_user_time");
        entity.HasOne(x => x.User)
            .WithMany(x => x.HistoryEvents)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDownloadJobs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DownloadJob>();
        entity.ToTable("download_jobs");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.Property(x => x.BookId).HasColumnName("book_id");
        entity.Property(x => x.MediaType).HasColumnName("media_type").HasConversion(MediaTypeConverter).IsRequired();
        entity.Property(x => x.Source).HasColumnName("source").IsRequired();
        entity.Property(x => x.ExternalJobId).HasColumnName("external_job_id");
        entity.Property(x => x.TorrentMagnet).HasColumnName("torrent_magnet");
        entity.Property(x => x.Status).HasColumnName("status").HasConversion(DownloadJobStatusConverter).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasDefaultValueSql("now()");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasDefaultValueSql("now()");
        entity.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
        entity.Property(x => x.FirstNotFoundAtUtc).HasColumnName("first_not_found_at_utc");
        entity.Property(x => x.FailureReason).HasColumnName("failure_reason");

        entity.HasIndex(x => new { x.UserId, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("ix_download_jobs_user_status_time");
        entity.HasIndex(x => new { x.UserId, x.BookId, x.MediaType })
            .HasDatabaseName("ux_active_download_per_user_book_type")
            .HasFilter("status IN ('queued', 'downloading')")
            .IsUnique();

        entity.HasOne(x => x.User)
            .WithMany(x => x.DownloadJobs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
