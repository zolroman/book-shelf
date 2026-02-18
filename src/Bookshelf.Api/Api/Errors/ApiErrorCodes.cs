namespace Bookshelf.Api.Api.Errors;

public static class ApiErrorCodes
{
    public const string QueryRequired = "QUERY_REQUIRED";
    public const string MediaTypeRequired = "MEDIA_TYPE_REQUIRED";
    public const string CandidateRequired = "CANDIDATE_REQUIRED";
    public const string InvalidArgument = "INVALID_ARGUMENT";

    public const string BookNotFound = "BOOK_NOT_FOUND";
    public const string DownloadNotFound = "DOWNLOAD_NOT_FOUND";
    public const string ShelfNotFound = "SHELF_NOT_FOUND";
    public const string CandidateNotFound = "CANDIDATE_NOT_FOUND";

    public const string ActiveDownloadExists = "ACTIVE_DOWNLOAD_EXISTS";
    public const string ShelfNameConflict = "SHELF_NAME_CONFLICT";
    public const string ShelfBookExists = "SHELF_BOOK_EXISTS";

    public const string FantlabUnavailable = "FANTLAB_UNAVAILABLE";
    public const string JackettUnavailable = "JACKETT_UNAVAILABLE";
    public const string QBittorrentUnavailable = "QBITTORRENT_UNAVAILABLE";
    public const string QBittorrentEnqueueFailed = "QBITTORRENT_ENQUEUE_FAILED";
    public const string QBittorrentStatusFailed = "QBITTORRENT_STATUS_FAILED";

    public const string DownloadNotFoundExternal = "DOWNLOAD_NOT_FOUND_EXTERNAL";
    public const string DownloadFailedProvider = "DOWNLOAD_FAILED_PROVIDER";
    public const string DownloadCancelFailed = "DOWNLOAD_CANCEL_FAILED";

    public const string NetworkRequired = "NETWORK_REQUIRED";
    public const string SyncFailedRetryable = "SYNC_FAILED_RETRYABLE";
    public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";

    public const string InternalError = "INTERNAL_ERROR";
    public const string StorageWriteFailed = "STORAGE_WRITE_FAILED";
    public const string StateTransitionInvalid = "STATE_TRANSITION_INVALID";
}
