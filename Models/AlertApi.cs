using System.Text;
using System.Text.Json;

namespace Relict_TelegramBot_Stride.Models
{
    public class AlertApi
    {
        private readonly HttpClient _http;

        public AlertApi(HttpClient http) => _http = http;
        private static readonly JsonSerializerOptions _json =
        new() { PropertyNameCaseInsensitive = true };

        public async Task<IReadOnlyList<AlertResponse>> GetAllAsync(CancellationToken ct = default)
        {
            using var res = await _http.GetAsync("/api/Alerts/alerts", ct);
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<IReadOnlyList<AlertResponse>>(stream, _json, ct)
                   ?? Array.Empty<AlertResponse>();
        }

        public async Task<bool> PostCitizenReportAsync(
            int alertId,
            object payload,
            CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await _http.PostAsync($"api/CitizenreportsControler/citizen-report?alertId={alertId}", content, ct);
            return res.IsSuccessStatusCode;
        }
    }
}
