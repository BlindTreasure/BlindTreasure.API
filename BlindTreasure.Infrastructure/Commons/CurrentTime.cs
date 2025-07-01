using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Infrastructure.Commons;

public class CurrentTime : ICurrentTime
{
    public DateTime GetCurrentTime()
    {
        return DateTime.UtcNow;
    }

}