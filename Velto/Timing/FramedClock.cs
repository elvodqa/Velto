using System.Diagnostics;
using OpenTK.Mathematics;

namespace Velto.Core.Timing;

public class FramedClock : IFrameBasedClock
{
    public IClock Source;

    public double CurrentTime { get; private set; }
    public double Rate => Source.Rate;
    public bool IsRunning => Source.IsRunning;
    
    public double ElapsedFrameTime => CurrentTime - LastFrameTime;
    public double AverageFrameTime { get; private set; }
    public double FramesPerSecond { get; private set; }
    public virtual double LastFrameTime { get; private set; }
    
    // for calculating fps
    private double timeUntilNextCalculation;
    private double timeSinceLastCalculation;
    private int framesSinceLastCalculation;

    public FramedClock() : this(new StopwatchClock(true)) { }
    public FramedClock(IClock source)
    {
        Source = source;
    }
    
    public void ProcessFrame()
    {
        framesSinceLastCalculation++;
        if (timeUntilNextCalculation <= 0)
        {
            timeUntilNextCalculation += 250f;
            if (framesSinceLastCalculation == 0) FramesPerSecond = 0;
            else FramesPerSecond = (int)Math.Ceiling(framesSinceLastCalculation * 1000f / timeSinceLastCalculation);
            timeSinceLastCalculation = 0;
            framesSinceLastCalculation = 0;
        }
        timeUntilNextCalculation -= ElapsedFrameTime;
        timeSinceLastCalculation += ElapsedFrameTime;
        AverageFrameTime = Damp(AverageFrameTime, ElapsedFrameTime, 0.01, ElapsedFrameTime / 1000.0);
        LastFrameTime = CurrentTime;
        CurrentTime = Source.CurrentTime;
    }
    
    public static double Lerp(double start, double final, double amount)
    {
        return start + (final - start) * amount;
    }

    public static double Damp(double start, double final, double smoothing, double delta)
    {
        Debug.Assert(smoothing >= 0.0 && smoothing <= 1.0);
        Debug.Assert(delta >= 0.0);
        return Lerp(start, final, 1f - (float)Math.Pow(smoothing, delta));
    }
    
    
}