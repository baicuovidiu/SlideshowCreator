namespace SlideshowCreator;

/// <summary>
/// Non-destructive capture-time correction used to align cameras whose clocks were not synchronized.
/// The original EXIF/video timestamp remains in SourceDate; only EffectiveDate participates in sorting.
/// </summary>
public static class CaptureTimeOffset
{
    public static DateTime Apply(DateTime sourceDate, double offsetSeconds)
    {
        if (!double.IsFinite(offsetSeconds))
            throw new ArgumentOutOfRangeException(nameof(offsetSeconds), "Offset must be finite.");

        try
        {
            return sourceDate.AddSeconds(offsetSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetSeconds), "Offset moves capture time outside the DateTime range.");
        }
    }
}
