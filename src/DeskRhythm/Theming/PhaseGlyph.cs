using System.Windows;
using System.Windows.Media;
using DeskRhythm.Core;

namespace DeskRhythm.Theming;

/// <summary>
/// A simple pictogram for each phase (person sitting on a chair, standing, walking).
/// Drawn as vector strokes on a 24-unit grid, so it is sharp at every DPI.
/// </summary>
public sealed class PhaseGlyph : FrameworkElement
{
    public static readonly DependencyProperty PhaseProperty = DependencyProperty.Register(
        nameof(Phase), typeof(Phase), typeof(PhaseGlyph),
        new FrameworkPropertyMetadata(Phase.Sitting, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(PhaseGlyph),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Dictionary<Phase, (Geometry Limbs, Point Head)> Shapes = new()
    {
        [Phase.Sitting] = (Geometry.Parse(
            "M9.5,8.2 L9.5,13.6 L15.6,13.6 L15.6,20.2 M9.5,9.8 L13.6,12.2 M5.4,8.6 L5.4,20.8 M5.4,16.2 L12.8,16.2 L12.8,20.8"),
            new Point(9.8, 4.6)),
        [Phase.Standing] = (Geometry.Parse(
            "M12,8.4 L12,14.4 M12,10 L8.6,13.2 M12,10 L15.4,13.2 M12,14.4 L9.4,20.6 M12,14.4 L14.6,20.6"),
            new Point(12, 4.6)),
        [Phase.Moving] = (Geometry.Parse(
            "M13,8.4 L11.6,14 M11.6,14 L9.2,17 L7.4,20.6 M11.6,14 L14,17.4 L14.8,20.6 M12.6,9.8 L9.2,12.2 M12.6,9.8 L15.8,12.8"),
            new Point(13.6, 4.6)),
    };

    static PhaseGlyph()
    {
        foreach (var shape in Shapes.Values)
        {
            shape.Limbs.Freeze();
        }
    }

    public Phase Phase
    {
        get => (Phase)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 24 : Math.Min(availableSize.Width, 24),
        double.IsInfinity(availableSize.Height) ? 24 : Math.Min(availableSize.Height, 24));

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
        {
            return;
        }

        var scale = size / 24.0;
        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var pen = new Pen(Foreground, 1.9)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        var (limbs, head) = Shapes[Phase];
        dc.DrawGeometry(null, pen, limbs);
        dc.DrawEllipse(Foreground, null, head, 2.1, 2.1);

        dc.Pop();
        dc.Pop();
    }
}
