using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NginxCacheFileReader;

AppContext.SetSwitch("System.IO.DisableFileLocking", true);

var rootCommand = new RootCommand("Nginx Cache File Reader - Read Nginx cache file info to a SQLite database");

var cachePathOption = new Option<string>("--cache-path")
{
	Description = "Path to the Nginx cache directory",
	Required = true
};

var dbPathOption = new Option<string>("--db-path")
{
	Description = "Path to the SQLite database file",
	Required = true
};

var includeHttpHeadersOption = new Option<bool>("--include-http-headers")
{
	Description = "Include HTTP headers in the database",
	DefaultValueFactory = _ => false
};

var includeBodyFileSignatureOption = new Option<bool>("--include-body-file-signature")
{
	Description = "Include body file signature (hex and ascii) in the database",
	DefaultValueFactory = _ => false
};

var degreeOfParallelismOption = new Option<int>("--degree-of-parallelism")
{
	Description = "Number of parallel workers for processing files",
	DefaultValueFactory = _ => 16
};

var pathsChannelCapacityOption = new Option<int>("--paths-channel-capacity")
{
	Description = "Capacity of the paths channel buffer",
	DefaultValueFactory = _ => 256
};

var itemsChannelCapacityOption = new Option<int>("--items-channel-capacity")
{
	Description = "Capacity of the items channel buffer",
	DefaultValueFactory = _ => 256
};

var sqlBatchSizeOption = new Option<int>("--sql-batch-size")
{
	Description = "Number of items to batch before writing to database",
	DefaultValueFactory = _ => 10000
};

var progressModuloOption = new Option<int>("--progress-modulo")
{
	Description = "Display progress every N items",
	DefaultValueFactory = _ => 10000
};

rootCommand.Add(cachePathOption);
rootCommand.Add(dbPathOption);
rootCommand.Add(includeHttpHeadersOption);
rootCommand.Add(includeBodyFileSignatureOption);
rootCommand.Add(degreeOfParallelismOption);
rootCommand.Add(pathsChannelCapacityOption);
rootCommand.Add(itemsChannelCapacityOption);
rootCommand.Add(sqlBatchSizeOption);
rootCommand.Add(progressModuloOption);
ExtraHelpAction.AddToRootCommand(rootCommand);

rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
{
	var cachePath = parseResult.GetValue(cachePathOption)!;
	var dbPath = parseResult.GetValue(dbPathOption)!;
	var includeHttpHeaders = parseResult.GetValue(includeHttpHeadersOption);
	var includeBodyFileSignature = parseResult.GetValue(includeBodyFileSignatureOption);
	var degreeOfParallelism = parseResult.GetValue(degreeOfParallelismOption);
	var pathsChannelCapacity = parseResult.GetValue(pathsChannelCapacityOption);
	var itemsChannelCapacity = parseResult.GetValue(itemsChannelCapacityOption);
	var sqlBatchSize = parseResult.GetValue(sqlBatchSizeOption);
	var progressModulo = parseResult.GetValue(progressModuloOption);

	var builder = Host.CreateApplicationBuilder();

	builder.Services.Configure<Args>(options =>
	{
		options.CachePath = cachePath;
		options.DbPath = dbPath;
		options.IncludeHttpHeaders = includeHttpHeaders;
		options.IncludeBodyFileSignature = includeBodyFileSignature;
		options.DegreeOfParallelism = degreeOfParallelism;
		options.PathsChannelCapacity = pathsChannelCapacity;
		options.ItemsChannelCapacity = itemsChannelCapacity;
		options.SqlBatchSize = sqlBatchSize;
		options.ProgressModulo = progressModulo;
	});

	builder.Services.AddDbContext<NginxCacheContext>(o => o
		.UseSqlite($"DataSource={dbPath};Foreign Keys=true")
		.UseSnakeCaseNamingConvention());

	builder.Logging.AddFilter("Microsoft.*", LogLevel.Warning);
	builder.Logging.ClearProviders();
	builder.Logging.AddSimpleConsole(o =>
	{
		o.IncludeScopes = true;
		o.SingleLine = true;
		o.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
	});

	builder.Services.AddHostedService<App>();

	var host = builder.Build();

	await using (var serviceScope = host.Services.CreateAsyncScope())
	await using (var dbContext = serviceScope.ServiceProvider.GetRequiredService<NginxCacheContext>())
	{
		await dbContext.Database.EnsureCreatedAsync(cancellationToken);
	}

	await host.RunAsync(token: cancellationToken);

	return 0;
});

return await rootCommand.Parse(args).InvokeAsync();


namespace NginxCacheFileReader
{
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
	}
}
