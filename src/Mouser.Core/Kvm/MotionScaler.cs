namespace Mouser.Core.Kvm;

public sealed class MotionScaler
{
    private double _remainder;

    public double Speed { get; set; } = 1.0; // 0.25 .. 4.0
    public bool Invert { get; set; }

    /// <summary>Scales a delta, carrying the fractional remainder so slow movement is not lost.</summary>
    public int Apply(int delta)
    {
        double v = delta * Speed + _remainder;
        int r = (int)Math.Truncate(v);
        _remainder = v - r;
        return Invert ? -r : r;
    }

    public void Reset()
    {
        _remainder = 0;
    }
}
