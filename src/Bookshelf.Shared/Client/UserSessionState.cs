namespace Bookshelf.Shared.Client;

public sealed class UserSessionState
{
    private long _userId = 1;

    public long UserId
    {
        get => _userId;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "userId must be greater than zero.");
            }

            _userId = value;
        }
    }
}
