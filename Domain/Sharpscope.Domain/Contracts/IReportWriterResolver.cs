using Sharpscope.Domain.Contracts;

public interface IReportWriterResolver
{
    IReportWriter Resolve(string format);
}

public sealed class ReportWriterResolver : IReportWriterResolver
{
    private readonly Dictionary<string, IReportWriter> _byFormat;

    public ReportWriterResolver(IEnumerable<IReportWriter> writers)
    {
        _byFormat = writers.ToDictionary(w => w.Format, StringComparer.OrdinalIgnoreCase);
    }

    public IReportWriter Resolve(string format)
    {
        if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("Format is required.", nameof(format));
        if (!_byFormat.TryGetValue(format, out var w))
            throw new NotSupportedException($"Unknown report format '{format}'. Supported: {string.Join(", ", _byFormat.Keys)}");
        return w;
    }
}
