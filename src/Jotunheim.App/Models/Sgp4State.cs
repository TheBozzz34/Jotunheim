namespace Jotunheim.App.Models;

internal sealed record Sgp4State(
    DateTime TimestampUtc,
    double Ds50Utc,
    double Mse,
    double LatDeg,
    double LonDeg,
    double AltKm,
    double PosX,
    double PosY,
    double PosZ,
    double VelX,
    double VelY,
    double VelZ);
