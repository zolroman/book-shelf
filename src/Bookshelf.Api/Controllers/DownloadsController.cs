using Bookshelf.Api.Mappers;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.Downloads;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DownloadsController(IDownloadService downloadService) : ControllerBase
{
    [HttpGet("candidates")]
    public async Task<ActionResult<IReadOnlyList<TorrentCandidateDto>>> GetCandidates(
        [FromQuery] string query,
        [FromQuery] int maxItems = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Query is required.");
        }

        var candidates = await downloadService.SearchCandidatesAsync(query, maxItems, cancellationToken);
        return Ok(candidates.Select(x => x.ToDto()).ToList());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DownloadJobDto>>> GetJobs(
        [FromQuery] int userId = 1,
        CancellationToken cancellationToken = default)
    {
        var jobs = await downloadService.GetJobsAsync(userId, cancellationToken);
        return Ok(jobs.Select(x => x.ToDto()).ToList());
    }

    [HttpGet("{jobId:int}")]
    public async Task<ActionResult<DownloadJobDto>> GetById(int jobId, CancellationToken cancellationToken = default)
    {
        var job = await downloadService.GetJobAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(job.ToDto());
    }

    [HttpPost("start")]
    public async Task<ActionResult<DownloadJobDto>> Start(
        [FromBody] StartDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        var started = await downloadService.StartAsync(request.UserId, request.BookFormatId, request.Source, cancellationToken);
        return Ok(started.ToDto());
    }

    [HttpPost("{jobId:int}/cancel")]
    public async Task<ActionResult<DownloadJobDto>> Cancel(int jobId, CancellationToken cancellationToken = default)
    {
        var canceled = await downloadService.CancelAsync(jobId, cancellationToken);
        if (canceled is null)
        {
            return NotFound();
        }

        return Ok(canceled.ToDto());
    }
}
