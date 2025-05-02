using System.Text;
using System.Text.Json;

namespace Relict_TelegramBot_Stride.Models
{
    public class NotificationsApi
    {
        private readonly HttpClient _http;

        private IReadOnlyList<RegionDto>? _regions;
        private DateTime _regionsFetchedAt;

        public NotificationsApi(HttpClient http) => _http = http;
        private static readonly JsonSerializerOptions _json =
        new() { PropertyNameCaseInsensitive = true };

       

        public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct = default)
        {
            if (_regions is { Count: > 0 } && DateTime.UtcNow - _regionsFetchedAt < TimeSpan.FromHours(1))
                return _regions;

            using var res = await _http.GetAsync("api/regions", ct);
            res.EnsureSuccessStatusCode();

            await using var s = await res.Content.ReadAsStreamAsync(ct);
            _regions = await JsonSerializer.DeserializeAsync<List<RegionDto>>(s, _json, ct) ?? [];
            _regionsFetchedAt = DateTime.UtcNow;
            return _regions;
        }

        public async Task<bool> PostSubscriptionAsync(SubscribePayload payload, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var res = await _http.PostAsync("api/subscribers/bot", content, ct);
            return res.IsSuccessStatusCode;
        }
    }
}
