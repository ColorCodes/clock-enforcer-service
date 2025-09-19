using System;

namespace ClockSessionService.Services
{
    public interface ISessionService
    {
        SessionInfo GetSessionStatus(string username);
        TimeSpan GetSessionDurationForLogin(int loginCount);
    }
}
