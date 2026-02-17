using Bookshelf.Api.Mappers;
using Bookshelf.Infrastructure.Services;
using Bookshelf.Shared.Contracts.Assets;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AssetsController(IBookshelfRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocalAssetDto>>> GetAssets(
        [FromQuery] int userId = 1,
        CancellationToken cancellationToken = default)
    {
        var assets = await repository.GetLocalAssetsAsync(userId, cancellationToken);
        return Ok(assets.Select(x => x.ToDto()).ToList());
    }

    [HttpPut]
    public async Task<ActionResult<LocalAssetDto>> UpsertAsset(
        [FromBody] UpsertLocalAssetRequest request,
        CancellationToken cancellationToken = default)
    {
        var asset = await repository.AddOrUpdateLocalAssetAsync(
            request.UserId,
            request.BookFormatId,
            request.LocalPath,
            request.FileSizeBytes,
            cancellationToken);

        return Ok(asset.ToDto());
    }

    [HttpDelete("{bookFormatId:int}")]
    public async Task<IActionResult> MarkDeleted(
        int bookFormatId,
        [FromQuery] int userId = 1,
        CancellationToken cancellationToken = default)
    {
        // Local asset deletion must not remove library/progress/history records.
        var removed = await repository.MarkLocalAssetDeletedAsync(userId, bookFormatId, cancellationToken);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }
}
