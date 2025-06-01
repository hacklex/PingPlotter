using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PingPlotter.Models;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace PingPlotter.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private PingService? _pingService;

    [ObservableProperty]
    private string _hostName = "google.com";
    
    [ObservableProperty]
    private bool _isPinging;
    
    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<PingResult> _pingResults = new();
    
    [ObservableProperty]
    private PingSession _currentSession = new();
    
    [ObservableProperty]
    private double _minResponseTime;
    
    [ObservableProperty]
    private double _maxResponseTime;
    
    [ObservableProperty]
    private double _avgResponseTime;
    
    [ObservableProperty]
    private int _packetLoss;
    
    [RelayCommand]
    private Task StartPinging()
    {
        if (IsPinging) return Task.CompletedTask;
        
        if (string.IsNullOrWhiteSpace(HostName))
        {
            StatusMessage = "Enter hostname";
            return Task.CompletedTask;
        }
        
        PingResults.Clear();
        CurrentSession = new PingSession
        {
            HostName = HostName,
            StartTime = DateTime.Now
        };
        
        StatusMessage = $"Pinging {HostName}...";
        IsPinging = true;
        
        _pingService = new PingService();
        
        Task.Run(async void () =>
        {
            try
            {
                await _pingService.ContinuousPingAsync(HostName, 1000, result =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        PingResults.Add(result);
                        CurrentSession.Results.Add(result);
                        UpdateStatistics();
                    });
                });
            }
            catch (Exception)
            {
                // we don't want to crash, sorry.
            }
        });
        return Task.CompletedTask;
    }
    
    [RelayCommand]
    private void StopPinging()
    {
        if (!IsPinging) return;
        
        _pingService?.Stop();
        IsPinging = false;
        StatusMessage = "Ping stopped";
    }

    public event Action<TopLevelRequest> TopLevelRequested = delegate { };
    
    [RelayCommand]
    private async Task SaveLogAsync()
    {
        if (PingResults.Count == 0)
        {
            StatusMessage = "No data to save";
            return;
        }
        
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filename = $"ping_log_{HostName}_{timestamp}.csv";

            var request = new TopLevelRequest();
            TopLevelRequested.Invoke(request);
            if (request.TopLevel == null) return;
            var file = await request.TopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = filename,
                Title = "Save Ping Log",
            });
            if (file == null) return;

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync("Timestamp,ResponseTime,Success,ErrorMessage");
            
            foreach (var result in PingResults)
            {
                var line = $"{result.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{result.ResponseTime},{result.Success},{result.ErrorMessage}";
                await writer.WriteLineAsync(line);
            }
            
            StatusMessage = $"Log saved to {filename}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save log: {ex.Message}";
        }
    }
    
    private void UpdateStatistics()
    {
        if (PingResults.Count == 0) return;
        
        var successfulPings = PingResults.Where(p => p.Success).ToList();
        
        if (successfulPings.Any())
        {
            MinResponseTime = successfulPings.Min(p => p.ResponseTime);
            MaxResponseTime = successfulPings.Max(p => p.ResponseTime);
            AvgResponseTime = successfulPings.Average(p => p.ResponseTime);
        }
        
        PacketLoss = PingResults.Count > 0 
            ? (int)((1 - (double)successfulPings.Count / PingResults.Count) * 100) 
            : 0;
    }
}
