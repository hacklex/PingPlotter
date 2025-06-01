using System;
using System.Collections.Generic;

namespace PingPlotter.Models;

public class PingSession
{
    public string HostName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public List<PingResult> Results { get; } = [];
}