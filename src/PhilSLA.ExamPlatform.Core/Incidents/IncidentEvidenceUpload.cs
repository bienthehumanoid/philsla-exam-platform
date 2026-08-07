namespace PhilSLA.ExamPlatform.Core.Incidents;

public sealed class IncidentEvidenceUpload
{
    public const long MaximumBytes = 10 * 1024 * 1024;

    private readonly Func<CancellationToken, Task<Stream>> _openReadAsync;

    public IncidentEvidenceUpload(
        string fileName,
        string mediaType,
        long length,
        Func<CancellationToken, Task<Stream>> openReadAsync)
    {
        FileName = RequireFileName(fileName);
        MediaType = RequireMediaType(mediaType, FileName);
        Length = length is > 0 and <= MaximumBytes
            ? length
            : throw new ArgumentOutOfRangeException(nameof(length));
        _openReadAsync = openReadAsync ?? throw new ArgumentNullException(nameof(openReadAsync));
    }

    public string FileName { get; }

    public string MediaType { get; }

    public long Length { get; }

    public async Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        await _openReadAsync(cancellationToken)
        ?? throw new InvalidOperationException("The evidence stream could not be opened.");

    private static string RequireFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A file name is required.", nameof(value));
        }

        return Path.GetFileName(value.Trim());
    }

    private static string RequireMediaType(string value, string fileName)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        var extension = Path.GetExtension(fileName);
        var valid = normalized switch
        {
            "image/jpeg" => extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase),
            "image/png" => extension.Equals(".png", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        return valid
            ? normalized!
            : throw new ArgumentException("Only JPEG and PNG evidence is supported.", nameof(value));
    }
}
