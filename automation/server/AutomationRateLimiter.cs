using System;
using Godot;

namespace Utopia.Automation;

/// <summary>Token bucket. Allow() consumes one token; refills continuously.</summary>
public class AutomationRateLimiter
{
    public double Capacity { get; }
    public double RefillPerSecond { get; }

    private double _tokens;
    private ulong _lastMsec;

    public AutomationRateLimiter(double capacity = AutomationProtocol.RateBurst, double refill = AutomationProtocol.RatePerSecond)
    {
        Capacity = capacity;
        RefillPerSecond = refill;
        _tokens = capacity;
        _lastMsec = Time.GetTicksMsec();
    }

    public bool Allow()
    {
        ulong now = Time.GetTicksMsec();
        _tokens = Math.Min(Capacity, _tokens + (now - _lastMsec) / 1000.0 * RefillPerSecond);
        _lastMsec = now;
        if (_tokens < 1.0)
            return false;
        _tokens -= 1.0;
        return true;
    }
}
