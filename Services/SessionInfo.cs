using System;

namespace ClockSessionService.Services
{
    public class SessionInfo
    {
        public bool IsActive { get; set; }
        public TimeSpan RemainingTime { get; set; }
        public bool CanRequestOvertime { get; set; }
        public int LoginCount { get; set; }
    }
}
