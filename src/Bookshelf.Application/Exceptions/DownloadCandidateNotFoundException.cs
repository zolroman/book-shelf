namespace Bookshelf.Application.Exceptions;

public sealed class DownloadCandidateNotFoundException : Exception
{
    public DownloadCandidateNotFoundException(string candidateId)
        : base($"Download candidate '{candidateId}' was not found.")
    {
        CandidateId = candidateId;
    }

    public string CandidateId { get; }
}
