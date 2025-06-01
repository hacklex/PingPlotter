using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace PingPlotter.Models;

public class PingService(int timeoutMs = 1000)
{
    private readonly CancellationTokenSource _cts = new();

    private async Task<PingResult> PingHostAsync(string hostNameOrAddress)
    {
        var result = new PingResult
        {
            Timestamp = DateTime.Now
        };
        
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(hostNameOrAddress, timeoutMs);
            
            result.Success = reply.Status == IPStatus.Success;
            result.ResponseTime = reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
            result.ErrorMessage = reply.Status != IPStatus.Success ? reply.Status.ToString() : null;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ResponseTime = -1;
            result.ErrorMessage = ex.Message;
        }
        result.FinishTimestamp = DateTime.Now;
        
        return result;
    }
    
    public async Task ContinuousPingAsync(string hostNameOrAddress, int delayBetweenPingsMs,
        Action<PingResult> onPingCompleted)
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var result = await PingHostAsync(hostNameOrAddress);
                onPingCompleted(result);
                
                await Task.Delay(delayBetweenPingsMs, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
    
    public void Stop()
    {
        _cts.Cancel();
    }
    
    public void Dispose()
    {
        _cts.Dispose();
    }
}