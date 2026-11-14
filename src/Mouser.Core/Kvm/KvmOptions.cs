namespace Mouser.Core.Kvm;

public sealed class KvmOptions
{
    public int EdgeResistanceMs { get; init; } = 80;
    public double EdgePushPixels { get; init; } = 15;

    public static KvmOptions FromFeel(string? feel) => feel switch
    {
        "Relaxed" => new() { EdgeResistanceMs = 0, EdgePushPixels = 1 },
        "Careful" => new() { EdgeResistanceMs = 300, EdgePushPixels = 60 },
        _ => new() // "Normal"
    };
}
