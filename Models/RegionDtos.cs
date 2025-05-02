namespace Relict_TelegramBot_Stride.Models
{
    public record RegionDto(int RegionId, string Name);
    public record SubscribePayload(long TelegramUserId, IReadOnlyList<int> RegionIds);
}
