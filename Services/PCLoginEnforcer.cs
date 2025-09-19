using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClockSessionService.Services;

namespace ClockSessionService.Services
{
    public class PCLoginEnforcer
    {
        private static readonly string BaseFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClockEnforcer");

        private static readonly string DebugLogPath =
            Path.Combine(BaseFolder, "enforcer_debug_log.txt");

        private readonly LogService logService = new LogService();
        private readonly string apiKey;
        private string authToken;

        public PCLoginEnforcer()
        {
            apiKey = Environment.GetEnvironmentVariable("SAASHR_API_KEY", EnvironmentVariableTarget.Machine);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Define SAASHR_API_KEY en las Environment Variables");
        }

        public void ForceUserLogOff()
        {
            _ = ForceLogOffAsync();
        }

        private async Task ForceLogOffAsync()
        {
            string systemUsername = Environment.UserName;

            try
            {
                int todayCount = logService.GetTodayLoginCount(systemUsername);
                if (todayCount % 2 == 0)
                {
                    File.AppendAllText(DebugLogPath,
                        $"{DateTime.Now}: Even number of punches. No punch-out needed.{Environment.NewLine}");
                }
                else
                {
                    var (clockinUsername, password) = logService.GetLastSavedCredentials(systemUsername);
                    if (!string.IsNullOrEmpty(clockinUsername) && !string.IsNullOrEmpty(password))
                    {
                        bool success = await AuthenticateUserAsync(clockinUsername, password);
                        if (!success)
                        {
                            File.AppendAllText(DebugLogPath,
                                $"{DateTime.Now}: Failed to authenticate {clockinUsername}{Environment.NewLine}");
                        }
                        else
                        {
                            string response = await SendPunchAsync();
                            File.AppendAllText(DebugLogPath,
                                $"{DateTime.Now}: Forced punch response: {response}{Environment.NewLine}");
                        }
                    }
                    else
                    {
                        File.AppendAllText(DebugLogPath,
                            $"{DateTime.Now}: No saved credentials for {systemUsername}{Environment.NewLine}");
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(DebugLogPath,
                    $"{DateTime.Now}: ERROR during logoff: {ex.Message}{Environment.NewLine}");
            }

            await Task.Delay(10000);
            Process.Start("shutdown", "/l");
        }

        private async Task<bool> AuthenticateUserAsync(string username, string password)
        {
            string loginUrl = "https://secure2.saashr.com/ta/rest/v1/login";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ClockEnforcerService");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Api-Key", apiKey);

            var payload = new
            {
                credentials = new { username, password, company = "AGI04" }
            };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(loginUrl, content);
                var respString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(respString);
                    if (doc.RootElement.TryGetProperty("token", out JsonElement tokenElement))
                    {
                        authToken = tokenElement.GetString();
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> SendPunchAsync()
        {
            if (string.IsNullOrWhiteSpace(authToken))
                return "No valid auth token.";

            string punchUrl = "https://secure2.saashr.com/ta/rest/v1/webclock";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Api-Key", apiKey);
            client.DefaultRequestHeaders.Add("User-Agent", "ClockEnforcerService");

            var payload = new { action = "punch" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(punchUrl, content);
                string result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                return $"Error sending punch: {ex.Message}";
            }
        }
    }
}
