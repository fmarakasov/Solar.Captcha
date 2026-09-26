using System;

namespace Solar.Captcha.Raster;

/// <summary>
/// How a linear gradient behaves outside the band between its two stops.
/// </summary>
public enum GradientRepetition
{
    /// <summary>Clamp to the nearest stop colour.</summary>
    None = 0,

    /// <summary>Restart the gradient from the first stop.</summary>
    Repeat = 1,

    /// <summary>Mirror the gradient on every additional band.</summary>
    Reflect = 2,
}

/// <summary>
/// A linear gradient between two points and two colours.
/// </summary>
public sealed class LinearGradientBrush
{
    /// <summary>Initializes a linear gradient.</summary>
    /// <param name="start">The point at which the gradient is at <paramref name="startColor"/>.</param>
    /// <param name="end">The point at which the gradient is at <paramref name="endColor"/>.</param>
    /// <param name="startColor">The colour at <paramref name="start"/>.</param>
    /// <param name="endColor">The colour at <paramref name="end"/>.</param>
    /// <param name="repetition">How the gradient behaves outside the two stops.</param>
    /// <exception cref="ArgumentException">When the two points coincide, leaving no gradient axis.</exception>
    public LinearGradientBrush(
        PointF start,
        PointF end,
        RasterColor startColor,
        RasterColor endColor,
        GradientRepetition repetition = GradientRepetition.None)
    {
        if (start == end)
        {
            throw new ArgumentException(
                "Gradient start and end points must differ; there is no axis to interpolate along.",
                nameof(end));
        }

        Start = start;
        End = end;
        StartColor = startColor;
        EndColor = endColor;
        Repetition = repetition;
    }

    /// <summary>Gets the point at which the gradient is at <see cref="StartColor"/>.</summary>
    public PointF Start { get; }

    /// <summary>Gets the point at which the gradient is at <see cref="EndColor"/>.</summary>
    public PointF End { get; }

    /// <summary>Gets the colour at <see cref="Start"/>.</summary>
    public RasterColor StartColor { get; }

    /// <summary>Gets the colour at <see cref="End"/>.</summary>
    public RasterColor EndColor { get; }

    /// <summary>Gets how the gradient behaves outside the two stops.</summary>
    public GradientRepetition Repetition { get; }

    /// <summary>
    /// Computes the colour at a position along the gradient axis.
    /// </summary>
    /// <param name="t">
    /// The position as a fraction of the axis: 0 is <see cref="Start"/>, 1 is <see cref="End"/>.
    /// Values outside the range are folded according to <see cref="Repetition"/>.
    /// </param>
    /// <returns>The interpolated colour.</returns>
    internal RasterColor ColorAt(float t)
    {
        var applied = ApplyRepetition(t);

        if (applied <= 0f)
        {
            return StartColor;
        }

        if (applied >= 1f)
        {
            return EndColor;
        }

        return new RasterColor(
            Lerp(StartColor.R, EndColor.R, applied),
            Lerp(StartColor.G, EndColor.G, applied),
            Lerp(StartColor.B, EndColor.B, applied),
            Lerp(StartColor.A, EndColor.A, applied));
    }

    private float ApplyRepetition(float t) => Repetition switch
    {
        GradientRepetition.Repeat => t - MathF.Floor(t),
        GradientRepetition.Reflect => FoldReflect(t),
        _ => t,
    };

    private static float FoldReflect(float t)
    {
        var folded = t - 2f * MathF.Floor(t / 2f);
        return folded > 1f ? 2f - folded : folded;
    }

    private static byte Lerp(byte from, byte to, float t) =>
        (byte)MathF.Round(from + (to - from) * t);
}
