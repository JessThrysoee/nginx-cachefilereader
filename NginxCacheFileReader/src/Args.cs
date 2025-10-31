using Microsoft.Extensions.Logging;

namespace NginxCacheFileReader;

public record Args
{
	public string CachePath { get; set; } = default!;
	public string DbPath { get; set; } = default!;
	public bool IncludeHttpHeaders { get; set; }
	public bool IncludeBodyFileSignature { get; set; }
	public int DegreeOfParallelism { get; set; }
	public int PathsChannelCapacity { get; set; }
	public int ItemsChannelCapacity { get; set; }
	public int SqlBatchSize { get; set; }
	public int ProgressModulo { get; set; }
	public LogLevel LogLevel { get; set; }
}