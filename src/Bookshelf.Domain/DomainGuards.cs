namespace Bookshelf.Domain;

public static class DomainGuards
{
    public static void RequirePercent(float percent, string parameterName)
    {
        if (percent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Progress percent must be in [0..100].");
        }
    }

    public static void RequireRating(float rating, string parameterName)
    {
        if (rating is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Rating must be in [0..10].");
        }
    }
}
