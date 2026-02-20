using System.Net;
using System.Security.Claims;
using Bookshelf.Api.Api.Errors;

namespace Bookshelf.Api.Api.Endpoints.Common;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        public long Id
        {
            get
            {
                var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue("userId")
                    ?? user.FindFirstValue("sub");

                if (long.TryParse(raw, out var parsed) && parsed > 0)
                {
                    return parsed;
                }

                throw new ApiException(
                    ApiErrorCodes.InvalidArgument,
                    "Authenticated user id claim is missing or invalid.",
                    HttpStatusCode.BadRequest);
            }
        }
    }
}
