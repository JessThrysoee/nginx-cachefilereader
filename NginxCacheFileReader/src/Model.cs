using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace NginxCacheFileReader;


public class NginxCacheContext(DbContextOptions<NginxCacheContext> options) : DbContext(options)
{
	public DbSet<CacheItem> CacheItems { get; set; }

	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder.Conventions.Remove<ForeignKeyIndexConvention>();

		base.ConfigureConventions(configurationBuilder);
	}
}

public class BloggingContextFactory : IDesignTimeDbContextFactory<NginxCacheContext>
{
	public NginxCacheContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<NginxCacheContext>();
		optionsBuilder
			.UseSqlite($"DataSource=:memory:;Foreign Keys=true")
			.UseSnakeCaseNamingConvention();

		return new NginxCacheContext(optionsBuilder.Options);
	}
}


public class CacheItem
{
	public int CacheItemId { get; set; }

	public string Key { get; set; } = default!;
	public int StatusCode { get; set; }
	public string Path { get; set; } = default!;
	public string? BodyFileSignatureHex { get; set; } = default!;
	public string? BodyFileSignatureAscii { get; set; } = default!;

	public CacheItemHeader CacheItemHeader { get; set; } = default!;
	public List<HttpHeader> HttpHeaders { get; set; } = [];
}


public class HttpHeader
{
	public HttpHeader() { }

	public int HttpHeaderId { get; set; }

	public string Name { get; set; } = default!;
	public string Value { get; set; } = default!;

	public int CacheItemId { get; set; }
	public CacheItem CacheItem { get; set; } = default!;
}


public class CacheItemHeader
{
	public CacheItemHeader() { }

	public CacheItemHeader(NginxCacheHeader h)
	{
		Version = (int)h.Version;
		ValidSec = DateTimeOffset.FromUnixTimeSeconds(h.ValidSec).UtcDateTime; ;
		UpdatingSec = DateTimeOffset.FromUnixTimeSeconds(h.UpdatingSec).UtcDateTime;
		ErrorSec = DateTimeOffset.FromUnixTimeSeconds(h.ErrorSec).UtcDateTime;
		LastModified = DateTimeOffset.FromUnixTimeSeconds(h.LastModified).UtcDateTime;
		Date = DateTimeOffset.FromUnixTimeSeconds(h.Date).UtcDateTime;
		Crc32 = (int)h.Crc32;
		ValidMsec = h.ValidMsec;
		Etag = Encoding.UTF8.GetString(h.Etag, 0, h.EtagLen);
		Vary = Encoding.UTF8.GetString(h.Vary, 0, h.VaryLen);
		Variant = Encoding.UTF8.GetString(h.Variant).TrimEnd('\0');
	}

	public int CacheItemHeaderId { get; set; }

	public int Version { get; set; }
	public DateTime? ValidSec { get; set; }
	public DateTime? UpdatingSec { get; set; }
	public DateTime? ErrorSec { get; set; }
	public DateTime? LastModified { get; set; }
	public DateTime? Date { get; set; }
	public int? Crc32 { get; set; }
	public long? ValidMsec { get; set; }
	public string? Etag { get; set; } = default!;
	public string? Vary { get; set; } = default!;
	public string? Variant { get; set; } = default!;

	public int CacheItemId { get; set; }
	public CacheItem CacheItem { get; set; } = default!;
}
