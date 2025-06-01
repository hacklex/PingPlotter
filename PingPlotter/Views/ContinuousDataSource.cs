using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using PingPlotter.ViewModels;
using ScottPlot;
using ScottPlot.DataSources;

namespace PingPlotter.Views;

public partial class ContinuousDataSource(IEnumerable<double>? initialXs, IEnumerable<double>? initialYs) : ViewModelBase, ISignalXYSource
{
    [ObservableProperty] private int _minimumIndex;
    [ObservableProperty] private int _maximumIndex = -1;
    [ObservableProperty] private double _xOffset;
    [ObservableProperty] private double _yOffset;
    [ObservableProperty] private bool _rotated;
  
    public List<double> Xs { get; } = [..initialXs ?? []];
    public List<double> Ys { get; } = [..initialYs ?? []];

    public void AddPoint(double x, double y)
    {
        Xs.Add(x);
        Ys.Add(y);
        MaximumIndex = Xs.Count - 1; 
    }

    public void Clear()
    {
        Xs.Clear();
        Ys.Clear();
        MaximumIndex = Xs.Count - 1;
    }
  
    public AxisLimits GetAxisLimits()
    {
        var range = GetRange(MinimumIndex, MaximumIndex);
        var coordinateRange = (Xs.Count > 0 && MaximumIndex >= MinimumIndex)
            ? new CoordinateRange(Xs[MinimumIndex] + XOffset, Xs[MaximumIndex] + XOffset)
            : CoordinateRange.NotSet;
        return !Rotated ? new AxisLimits(coordinateRange, range) : new AxisLimits(range, coordinateRange);
    }

    public Pixel[] GetPixelsToDraw(RenderPack rp, IAxes axes)
    {
        return !Rotated ? GetPixelsToDrawHorizontally(rp, axes) : GetPixelsToDrawVertically(rp, axes);
    }

    private Pixel[] GetPixelsToDrawHorizontally(RenderPack rp, IAxes axes)
    {
        var (pointsBefore1, num1) = GetFirstPointX(axes);
        var (pointsBefore2, num2) = GetLastPointX(axes);
        var visibleRange = new IndexRange(num1, num2);
        var pixels = Enumerable.Range(0, (int) Math.Ceiling(rp.DataRect.Width))
            .Select((Func<int, IEnumerable<Pixel>>) (pxColumn => GetColumnPixelsX(pxColumn, visibleRange, rp, axes)))
            .SelectMany((Func<IEnumerable<Pixel>, IEnumerable<Pixel>>) (x => x));
        List<Pixel> pixelList = [..pointsBefore1, ..pixels, ..pointsBefore2];
        var array = pixelList.ToArray();
        if (pointsBefore1.Length != 0)
            SignalInterpolation.InterpolateBeforeX(rp, array);
        if (pointsBefore2.Length != 0)
            SignalInterpolation.InterpolateAfterX(rp, array);
        return array;
    }

    private Pixel[] GetPixelsToDrawVertically(RenderPack rp, IAxes axes)
    {
        var (pointsBefore1, num1) = GetFirstPointY(axes);
        var (pointsBefore2, num2) = GetLastPointY(axes);
        var visibleRange = new IndexRange(num1, num2);
        var pixels = Enumerable.Range(0, (int) Math.Ceiling(rp.DataRect.Height)).Select((Func<int, IEnumerable<Pixel>>) (pxRow => GetColumnPixelsY(pxRow, visibleRange, rp, axes))).SelectMany((Func<IEnumerable<Pixel>, IEnumerable<Pixel>>) (x => x));
        List<Pixel> pixelList = [..pointsBefore1, ..pixels, ..pointsBefore2];
        var array = pixelList.ToArray();
        if (pointsBefore1.Length != 0)
            SignalInterpolation.InterpolateBeforeY(rp, array);
        if (pointsBefore2.Length != 0)
            SignalInterpolation.InterpolateAfterY(rp, array);
        return array;
    }

    private CoordinateRange GetRange(int index1, int index2)
    {
        if (index1 < 0 || index1 >= Ys.Count || index2 < 0 || index2 >= Ys.Count)
            return CoordinateRange.NotSet;
        var value1 = Ys[index1];
        var value2 = Ys[index1];
        for (var index = index1; index <= index2; ++index)
        {
            value1 = Math.Min(Ys[index], value1);
            value2 = Math.Max(Ys[index], value2);
        }
        return new CoordinateRange(value1 + YOffset, value2 + YOffset);
    }

    private int GetIndex(double x)
    {
        var indexRange = new IndexRange(MinimumIndex, MaximumIndex);
        return GetIndex(x, indexRange);
    }

    private int GetIndex(double x, IndexRange indexRange)
    {
        var index = Xs.BinarySearch(indexRange.Min, indexRange.Length, x - XOffset, Comparer<double>.Default);
        if (index < 0)
            index = ~index;
        return index;
    }

    private IEnumerable<Pixel> GetColumnPixelsX(
        int pixelColumnIndex,
        IndexRange rng,
        RenderPack rp,
        IAxes? axes)
    {
        if (axes?.XAxis == null) yield break;
        var xPixel = pixelColumnIndex + rp.DataRect.Left;
        var num = axes.XAxis.Width / rp.DataRect.Width;
        var x1 = axes.XAxis.Min + num * pixelColumnIndex;
        var x2 = x1 + num;
        var startIndex = GetIndex(x1, rng);
        var endIndex = GetIndex(x2, rng);
        var pointsInRange = endIndex - startIndex;
        if (pointsInRange == 0) yield break;
        yield return new Pixel(xPixel, axes.GetPixelY(Ys[startIndex] + YOffset));
        if (pointsInRange <= 1) yield break;
        var yRange = GetRange(startIndex, endIndex - 1);
        yield return new Pixel(xPixel, axes.GetPixelY(yRange.Min));
        yield return new Pixel(xPixel, axes.GetPixelY(yRange.Max));
        yield return new Pixel(xPixel, axes.GetPixelY(Ys[endIndex - 1] + YOffset));
    }

    private IEnumerable<Pixel> GetColumnPixelsY(
        int rowColumnIndex,
        IndexRange rng,
        RenderPack rp,
        IAxes? axes)
    {
        if (axes?.YAxis == null) yield break;
        var yPixel = rp.DataRect.Bottom - rowColumnIndex;
        var num = axes.YAxis.Height / rp.DataRect.Height;
        var x1 = axes.YAxis.Min + num * rowColumnIndex;
        var x2 = x1 + num;
        var startIndex = GetIndex(x1, rng);
        var endIndex = GetIndex(x2, rng);
        var pointsInRange = endIndex - startIndex;
        if (pointsInRange != 0)
        {
            yield return new Pixel(axes.GetPixelX(Ys[startIndex] + XOffset), yPixel);
            if (pointsInRange > 1)
            {
                var yRange = GetRange(startIndex, endIndex - 1);
                yield return new Pixel(axes.GetPixelX(yRange.Min), yPixel);
                yield return new Pixel(axes.GetPixelX(yRange.Max), yPixel);
                yield return new Pixel(axes.GetPixelX(Ys[endIndex - 1] + XOffset), yPixel);
            }
        }
    }

    private (Pixel[] pointsBefore, int firstIndex) GetFirstPointX(IAxes? axes)
    {
        if (axes?.XAxis == null) return ([], MinimumIndex);
        var index = GetIndex(axes.XAxis.Min);
        if (index <= MinimumIndex)
            return ([], MinimumIndex);
        return ([
            new Pixel(axes.GetPixelX(Xs[index - 1] + XOffset), axes.GetPixelY(Ys[index - 1] + YOffset))
        ], index);
    }

    private (Pixel[] pointsBefore, int firstIndex) GetFirstPointY(IAxes? axes)
    {
        if (axes?.YAxis == null) return ([], MinimumIndex);
        var index = GetIndex(axes.YAxis.Min);
        if (index <= MinimumIndex)
            return ([], MinimumIndex);
        return ([
            new Pixel(axes.GetPixelX(Ys[index - 1] + XOffset), axes.GetPixelY(Xs[index - 1] + YOffset))
        ], index);
    }

    private (Pixel[] pointsBefore, int lastIndex) GetLastPointX(IAxes? axes)
    {
        if (axes?.XAxis == null) return ([], MaximumIndex);
        var index = GetIndex(axes.XAxis.Max);
        if (index > MaximumIndex)
            return ([], MaximumIndex);
        return ([
            new Pixel(axes.GetPixelX(Xs[index] + XOffset), axes.GetPixelY(Ys[index] + YOffset))
        ], index);
    }

    private (Pixel[] pointsBefore, int lastIndex) GetLastPointY(IAxes? axes)
    {
        if (axes?.YAxis == null) return ([], MaximumIndex);
        var index = GetIndex(axes.YAxis.Max);
        if (index > MaximumIndex)
            return ([], MaximumIndex);
        return ([
            new Pixel(axes.GetPixelX(Ys[index] + XOffset), axes.GetPixelY(Xs[index] + YOffset))
        ], index);
    }
}