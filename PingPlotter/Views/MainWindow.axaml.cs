using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Styling;
using PingPlotter.ViewModels;
using ScottPlot;
using ScottPlot.Plottables;

namespace PingPlotter.Views;

public partial class MainWindow : Window
{
    private readonly double[] _times = [];
    private readonly double[] _values = [];
    private SignalXY? _scatterPlot;

    public void UpdatePlotTheme()
    { 
        if (ActualThemeVariant == ThemeVariant.Dark)
        {
                

            PingPlot.Plot.Style.Background(Color.FromHex("#181818"), Color.FromHex("#1f1f1f"));
            PingPlot.Plot.Style.ColorAxes(Color.FromHex("#d7d7d7"));
            PingPlot.Plot.Style.ColorGrids(Color.FromHex("#404040"));
            PingPlot.Plot.Style.ColorLegend(Color.FromHex("#404040"), Color.FromHex("#d7d7d7"), Color.FromHex("#d7d7d7"));
        }
        else
        {
            PingPlot.Plot.Style.Background(Colors.White, Colors.White);
            PingPlot.Plot.Style.ColorAxes(Color.FromHex("#333333"));
            PingPlot.Plot.Style.ColorGrids(Color.FromHex("#e5e5e5"));
            PingPlot.Plot.Style.ColorLegend(Color.FromHex("#e5e5e5"), Color.FromHex("#333333"), Color.FromHex("#333333"));
        }
    }
    
    public MainWindow()
    {
        InitializeComponent();
        
        SetupPlot();

        ActualThemeVariantChanged += (sender, args) => UpdatePlotTheme();
            
        UpdatePlotTheme();
        
        DataContextChanged += OnDataContextChanged;

        if (DateTime.Now < DateTime.MinValue)
            Program.BuildAvaloniaApp(); // No, it is NOT unused.
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        vm.PropertyChanged += (_, changedArgs) =>
        {
            switch (changedArgs.PropertyName)
            {
                case nameof(MainWindowViewModel.PingResults):
                    UpdatePlot();
                    break;
            }
        };
        vm.PingResults.CollectionChanged += (_, _) => UpdatePlot();
        vm.TopLevelRequested += request => request.TopLevel = GetTopLevel(this);
    }
    
    private ContinuousDataSource? _dataSource;

    private void SetupPlot()
    {
        PingPlot.Plot.Title("Ping Response Time");
        PingPlot.Plot.YLabel("Response Time (ms)");
        PingPlot.Plot.XLabel("Time (seconds)");

        _dataSource = new ContinuousDataSource(_times, _values);
        _scatterPlot = new SignalXY(_dataSource); 
        PingPlot.Plot.PlottableList.Add(_scatterPlot);
        
        _scatterPlot.LineWidth = 1;
        
        var horizontalLine = PingPlot.Plot.Add.HorizontalLine(0);
        horizontalLine.LineWidth = 1; 
        
        PingPlot.Refresh();
    }

    private void UpdatePlot()
    {
        if (DataContext is not MainWindowViewModel vm
            || vm.PingResults.Count == 0
            || _dataSource is null) return; 
        
        PingPlot.Plot.Clear();

        if (vm.CurrentSession.Results.Count == 0) return;
        
        var startTime = vm.PingResults.First().Timestamp;
        var times = new double[vm.PingResults.Count];
        var values = new double[vm.PingResults.Count];

        if (_dataSource.Xs.Count > vm.PingResults.Count 
            || _dataSource.Ys.Count > vm.PingResults.Count)
            _dataSource.Clear();
        
        for (var i = _dataSource.Xs.Count; i < vm.PingResults.Count; i++)
        {
            var result = vm.PingResults[i];
            var elapsedTime = (result.Timestamp - startTime).TotalSeconds;
            times[i] = elapsedTime;
            values[i] = result.Success ? result.ResponseTime : 0;
            _dataSource.AddPoint(times[i], result.Success ? values[i] : 1000);
        }
        var line = new SignalXY(_dataSource);
        line.Color = (ActualThemeVariant == ThemeVariant.Dark) ? Colors.White : Colors.Black;
        PingPlot.Plot.PlottableList.Add(line);
        line.LineWidth = 1;
        
        if (AutoScaleCheckBox.IsChecked == true)
            PingPlot.Plot.Axes.AutoScale();
        
        PingPlot.Refresh();
    }
}
