using ClockSessionService.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISessionService sessionService;

    private static readonly string BaseFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClockEnforcer");

    private static readonly string FlagFilePath =
        Path.Combine(BaseFolder, "no_session_detected.txt");

    private static readonly string LogFilePath =
        Path.Combine(BaseFolder, "session_service_log.txt");

    private readonly LogService logService = new LogService();

    private bool overtimeGranted = false;
    private bool overtimeChecked = false;

    static Worker()
    {
        if (!Directory.Exists(BaseFolder))
            Directory.CreateDirectory(BaseFolder);
    }

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        sessionService = new SessionService(logService);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                string username = Environment.UserName;
                var session = sessionService.GetSessionStatus(username);

                if (session.IsActive)
                {
                    if (File.Exists(FlagFilePath))
                        File.Delete(FlagFilePath);

                    overtimeChecked = false;
                    overtimeGranted = false;

                    File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] Session active for {username}. Remaining time: {session.RemainingTime:c}{Environment.NewLine}");
                }
                else
                {
                    if (!File.Exists(FlagFilePath))
                    {
                        File.WriteAllText(FlagFilePath, DateTime.Now.ToString("O"));
                        File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] No active session for {username}. Starting 2-minute countdown.{Environment.NewLine}");
                    }
                    else
                    {
                        if (!overtimeChecked && logService.GetTodayLoginCount(username) >= 2)
                        {
                            overtimeChecked = true;
                            overtimeGranted = await CheckOvertimeStatus(username);
                        }

                        if (overtimeGranted)
                        {
                            File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] Overtime granted for {username}. Skipping logoff.{Environment.NewLine}");
                        }
                        else
                        {
                            DateTime detectedAt = DateTime.Parse(File.ReadAllText(FlagFilePath));
                            TimeSpan elapsed = DateTime.Now - detectedAt;

                            if (elapsed >= TimeSpan.FromMinutes(2))
                            {
                                File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] 2 minutes elapsed without active session for {username}. Enforcing logoff.{Environment.NewLine}");
                                new PCLoginEnforcer().ForceUserLogOff();
                            }
                            else
                            {
                                double minutesLeft = Math.Round(2 - elapsed.TotalMinutes, 1);
                                File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] {minutesLeft} minutes remaining before logoff for {username}.{Environment.NewLine}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] ERROR: {ex.Message}{Environment.NewLine}");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task<bool> CheckOvertimeStatus(string username)
    {
        try
        {
            string url = $"https://clock.adrianas.com/backend/timeclock/overtime/today?ad_username={username}";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();

            File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] Overtime API response: {json}{Environment.NewLine}");

            if (!response.IsSuccessStatusCode)
                return false;

            var result = JsonSerializer.Deserialize<OvertimeTodayResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result?.data?.accepted == true;
        }
        catch (Exception ex)
        {
            File.AppendAllText(LogFilePath, $"[{DateTime.Now:O}] ERROR while checking overtime: {ex.Message}{Environment.NewLine}");
            return false;
        }
    }

    private class OvertimeTodayResponse
    {
        public int status { get; set; }
        public string? label { get; set; }
        public OvertimeData? data { get; set; }
    }

    private class OvertimeData
    {
        public bool accepted { get; set; }
        public decimal hours { get; set; }
    }
}
