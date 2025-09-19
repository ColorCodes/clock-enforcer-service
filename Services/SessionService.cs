using System;

namespace ClockSessionService.Services
{
    public class SessionService : ISessionService
    {
        private readonly LogService _logService;
        private readonly DateTime _appStartTime;

        public SessionService(LogService logService)
        {
            _logService = logService;
            _appStartTime = DateTime.Now;
        }

        public SessionInfo GetSessionStatus(string username)
        {
            var loginTime = _logService.GetTodayLoginTime(username);
            var clockOutTime = _logService.GetTodayClockOutTime(username);
            int loginCount = _logService.GetTodayLoginCount(username);
            DateTime now = DateTime.Now;

            // Case: user has already completed full day (4 punches)
            if (loginCount >= 4)
                return InactiveSession(loginCount);

            // Case: BEFORE FIRST CLOCK-IN
            if (loginCount == 0)
                return GraceFromAppStart(TimeSpan.FromMinutes(2), loginCount);

            // Case: AFTER LUNCH (LOGIN = 2), wait 30 minutes since last clockout
            if (loginCount == 2)
            {
                if (!clockOutTime.HasValue)
                    return InactiveSession(loginCount);

                TimeSpan sinceClockOut = now - clockOutTime.Value;

                if (sinceClockOut < TimeSpan.FromMinutes(30))
                    return InactiveSession(loginCount);

                // Allow 2-minute grace period after lunch requirement is met
                return GraceFromAppStart(TimeSpan.FromMinutes(2), loginCount);
            }

            // Case: session ended (login <= clockout), check if grace applies
            if (!loginTime.HasValue || (clockOutTime.HasValue && loginTime <= clockOutTime))
                return InactiveSession(loginCount);

            // Session is active
            TimeSpan totalDuration = GetSessionDurationForLogin(loginCount);
            TimeSpan elapsed = now - loginTime.Value;
            TimeSpan remaining = totalDuration - elapsed;
            bool isActive = remaining > TimeSpan.Zero;

            return new SessionInfo
            {
                IsActive = isActive,
                RemainingTime = isActive ? remaining : TimeSpan.Zero,
                CanRequestOvertime = loginCount >= 3,
                LoginCount = loginCount
            };
        }

        private SessionInfo GraceFromAppStart(TimeSpan graceDuration, int loginCount)
        {
            TimeSpan elapsed = DateTime.Now - _appStartTime;
            TimeSpan remaining = graceDuration - elapsed;

            if (remaining <= TimeSpan.Zero)
                return InactiveSession(loginCount);

            return new SessionInfo
            {
                IsActive = true,
                RemainingTime = remaining,
                CanRequestOvertime = false,
                LoginCount = loginCount
            };
        }

        private SessionInfo InactiveSession(int loginCount)
        {
            return new SessionInfo
            {
                IsActive = false,
                RemainingTime = TimeSpan.Zero,
                CanRequestOvertime = false,
                LoginCount = loginCount
            };
        }

        public TimeSpan GetSessionDurationForLogin(int loginCount)
        {
            return loginCount == 1
                ? TimeSpan.FromMinutes(290)      // Simulated 4h50m
                : TimeSpan.FromMinutes(170);   // Simulated 2h50m
        }
    }
}
