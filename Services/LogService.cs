using System;
using System.IO;
using System.Linq;

namespace ClockSessionService.Services
{
    public class LogService
    {
        // Shared application-wide folder under C:\ProgramData
        private static readonly string BaseFolder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ClockEnforcer"
            );

        private static readonly string LogFilePath =
            Path.Combine(BaseFolder, "user_logins.txt");

        static LogService()
        {
            if (!Directory.Exists(BaseFolder))
                Directory.CreateDirectory(BaseFolder);
        }

        public DateTime? GetTodayLoginTime(string username)
        {
            if (!File.Exists(LogFilePath)) return null;

            return File.ReadAllLines(LogFilePath)
                .Where(line => line.StartsWith(username + ",LOGIN"))
                .Select(line => line.Split(','))
                .Where(parts => parts.Length == 3 && DateTime.TryParse(parts[2], out _))
                .Select(parts => DateTime.Parse(parts[2]))
                .Where(dt => dt.Date == DateTime.Today)
                .OrderByDescending(dt => dt)
                .FirstOrDefault();
        }

        public DateTime? GetTodayClockOutTime(string username)
        {
            if (!File.Exists(LogFilePath)) return null;

            return File.ReadAllLines(LogFilePath)
                .Where(line => line.StartsWith(username + ",CLOCKOUT"))
                .Select(line => line.Split(','))
                .Where(parts => parts.Length == 3 && DateTime.TryParse(parts[2], out _))
                .Select(parts => DateTime.Parse(parts[2]))
                .Where(dt => dt.Date == DateTime.Today)
                .OrderByDescending(dt => dt)
                .FirstOrDefault();
        }

        public int GetTodayLoginCount(string username)
        {
            if (!File.Exists(LogFilePath)) return 0;

            return File.ReadAllLines(LogFilePath)
                .Where(line => line.StartsWith(username + ",LOGIN"))
                .Select(line => line.Split(','))
                .Where(parts => parts.Length == 3
                                && DateTime.TryParse(parts[2], out var dt)
                                && dt.Date == DateTime.Today)
                .Count();
        }
        public (string, string) GetLastSavedCredentials(string username)
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClockEnforcer", "user_credentials.txt");

            if (!File.Exists(path))
                return (null, null);

            var lines = File.ReadAllLines(path)
                .Where(l => l.StartsWith(username + ","))
                .Select(l => l.Split(','))
                .Where(parts => parts.Length >= 3 && !string.IsNullOrEmpty(parts[1]))
                .Select(parts => (User: parts[0], Pass: parts[1], Timestamp: DateTime.Parse(parts[2])))
                .OrderByDescending(entry => entry.Timestamp)
                .ToList();

            if (lines.Count == 0)
                return (null, null);

            return (lines[0].User, lines[0].Pass);
        }

    }
}
