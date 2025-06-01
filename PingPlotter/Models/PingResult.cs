using System;

namespace PingPlotter.Models;

public class PingResult
{
    public DateTime Timestamp { get; set; }
    public DateTime FinishTimestamp { get; set; }
    public long ResponseTime { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}