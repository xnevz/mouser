using Mouser.Core.Kvm;
using Xunit;

namespace Mouser.Tests;

public sealed class MotionScalerTests
{
    [Fact]
    public void SpeedHalf_OverTenDeltasOfOne_SumsToFive()
    {
        var scaler = new MotionScaler { Speed = 0.5 };
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            sum += scaler.Apply(1);
        }
        Assert.Equal(5, sum);
    }

    [Fact]
    public void SpeedTwo_DoublesDeltas()
    {
        var scaler = new MotionScaler { Speed = 2.0 };
        Assert.Equal(6, scaler.Apply(3));
        Assert.Equal(-8, scaler.Apply(-4));
    }

    [Fact]
    public void Invert_FlipsSign()
    {
        var normal = new MotionScaler { Speed = 1.0, Invert = false };
        var inverted = new MotionScaler { Speed = 1.0, Invert = true };

        Assert.Equal(5, normal.Apply(5));
        Assert.Equal(-5, inverted.Apply(5));

        Assert.Equal(-7, normal.Apply(-7));
        Assert.Equal(7, inverted.Apply(-7));
    }

    [Fact]
    public void Remainder_NeverAccumulatesPastOne()
    {
        var scaler = new MotionScaler { Speed = 0.33 };
        for (int i = 0; i < 100; i++)
        {
            scaler.Apply(1);
            // After each call, if we pass delta 0, the output must be 0 because remainder is < 1.
            var clone = new MotionScaler { Speed = scaler.Speed };
            // Let's test that applying a series of fractional deltas never causes remainder to exceed 1
            // By testing delta=1 repeatedly, delta * Speed is 0.33. If remainder ever exceeded 1, a single delta of 0 would produce != 0.
        }

        // Test with different speeds
        double[] speeds = [0.1, 0.25, 0.333, 0.7, 1.25, 2.5, 3.75];
        foreach (var speed in speeds)
        {
            var s = new MotionScaler { Speed = speed };
            for (int i = 0; i < 50; i++)
            {
                s.Apply(3);
                int empty = s.Apply(0);
                Assert.Equal(0, empty); // Remainder alone must never be >= 1.0 or <= -1.0
            }
        }
    }
}
