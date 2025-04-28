namespace Relict_TelegramBot_Stride.Models
{
    public record CrimeDate(
        string Date,
        string Time
     );

    public record Victim(
        string VictimName,
        int VictimAge,
        string VictimGender,
        string VictimSkinColor,
        string VictimHair,
        string VictimClothing,
        string VictimDistinctiveFeatures,
        string VictimPhoto
    );

    public record Abductor(
        string AbductorName,
        int AbductorAge,
        string AbductorGender,
        string AbductorSkinColor,
        string AbductorHair,
        string AbductorClothing,
        string AbductorDistinctiveFeatures,
        string AbductorVehicle,
        string AbductorPhoto
    );

    public record AlertResponse(
        int AlertId,
        int AlertStatusId,
        string AlertStatus,
        string CrimeDistrict,
        string CrimeLocation,
        CrimeDate CrimeDate,
        Victim Victim,
        Abductor Abductor
    );
}
