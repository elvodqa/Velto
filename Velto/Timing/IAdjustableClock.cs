namespace Velto.Core.Timing;

public interface IAdjustableClock : IClock
{
    void Reset();

    void Start();

    void Stop();

    bool Seek(double position);
}