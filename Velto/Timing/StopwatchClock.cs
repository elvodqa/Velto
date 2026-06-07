using System.Diagnostics;

namespace Velto.Core.Timing;

public class StopwatchClock : Stopwatch, IAdjustableClock
{
    private double rate = 1.0;
    private double seekOffset;
    private double rateChangeUsed;
    private double rateChangeAccumulated;
    public double CurrentTime => (ElapsedMilliseconds - rateChangeUsed) * rate + rateChangeAccumulated + seekOffset;

    public double Rate
    {
        get
        {
            return rate;
        }
        set
        {
            if (rate != value)
            {
                rateChangeAccumulated += (ElapsedMilliseconds - rateChangeUsed) * rate;
                rateChangeUsed = ElapsedMilliseconds;
                rate = value;
            }
        }
    }

    public StopwatchClock(bool start = false)
    {
        if (start)
        {
            Start();
        }
    }

    public bool Seek(double position)
    {
        seekOffset = 0.0;
        seekOffset = position - CurrentTime;
        return true;
    }
}