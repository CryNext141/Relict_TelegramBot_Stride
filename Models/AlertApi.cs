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
            using var res = await _http.GetAsync("api/Alerts/alerts", ct); // ← без ?statusId
            res.EnsureSuccessStatusCode();

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<IReadOnlyList<AlertResponse>>(stream, _json, ct)
                   ?? Array.Empty<AlertResponse>();
        }
    }
}
