using Bookshelf.Api.Mappers;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.Downloads;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DownloadsController(IDownloadService downloadService) : ControllerBase
{
    private readonly IDownloadService _downloadService = downloadService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DownloadJobDto>>> GetJobs(
        [FromQuery] int userId = 1,
        CancellationToken cancellationToken = default)
    {
        var jobs = await _downloadService.GetJobsAsync(userId, cancellationToken);
        return Ok(jobs.Select(x => x.ToDto()).ToList());
    }

    [HttpGet("{jobId:int}")]
    public async Task<ActionResult<DownloadJobDto>> GetById(int jobId, CancellationToken cancellationToken = default)
    {
        var job = await _downloadService.GetJobAsync(jobId, cancellationToken);
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
        var started = await _downloadService.StartAsync(request.UserId, request.BookFormatId, request.Source, cancellationToken);
        return Ok(started.ToDto());
    }

    [HttpPost("{jobId:int}/cancel")]
    public async Task<ActionResult<DownloadJobDto>> Cancel(int jobId, CancellationToken cancellationToken = default)
    {
        var canceled = await _downloadService.CancelAsync(jobId, cancellationToken);
        if (canceled is null)
        {
            return NotFound();
        }

        return Ok(canceled.ToDto());
    }
}
