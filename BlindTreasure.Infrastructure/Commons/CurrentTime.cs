using VaccinaCare.Repository.Interfaces;

namespace VaccinaCare.Repository.Commons;

public class CurrentTime : ICurrentTime
{
    public DateTime GetCurrentTime()
    {
        return DateTime.UtcNow.AddHours(7);
    }
}