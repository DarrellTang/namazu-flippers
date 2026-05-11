namespace NamazuFlippers.Data;

public sealed class SessionState
{
    public Dictionary<int, bool> Bought { get; set; } = new();

    public Dictionary<int, bool> Listed { get; set; } = new();
}
