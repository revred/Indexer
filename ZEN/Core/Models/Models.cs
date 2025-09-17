namespace IndexContainment.Core.Models;

public readonly record struct Bar(DateTime T, decimal O, decimal H, decimal L, decimal C, long V);
public sealed record DayBars(DateTime D, List<Bar> Bars, decimal PrevClose);

public sealed record DailyRow(
    DateTime Date, decimal PrevClose, decimal Open, decimal P10,
    decimal LowAfter10, decimal HighAfter10, decimal Close,
    decimal GapPct, decimal ExtraDropPct, decimal ExtraRisePct, int TimeToLowMins,
    int Qual_1_0, int Hold_1_0, decimal VR_1_0,
    int Qual_1_5, int Hold_1_5, decimal VR_1_5,
    int Qual_2_0, int Hold_2_0, decimal VR_2_0,
    int Qual_3_0, int Hold_3_0, decimal VR_3_0,
    int Qual_4_0, int Hold_4_0, decimal VR_4_0
);