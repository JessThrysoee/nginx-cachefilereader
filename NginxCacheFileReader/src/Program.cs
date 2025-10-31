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

var logLevelOption = new Option<LogLevel>("--log-level")
{
	Description = "Logging level (Trace|Debug|Information|Warning|Error|Critical|None)",
	Arity = ArgumentArity.ExactlyOne,
	DefaultValueFactory = _ => LogLevel.Information
};

rootCommand.Add(cachePathOption);
rootCommand.Add(dbPathOption);
rootCommand.Add(includeHttpHeadersOption);
rootCommand.Add(includeBodyFileSignatureOption);
rootCommand.Add(degreeOfParallelismOption);
rootCommand.Add(sqlBatchSizeOption);
rootCommand.Add(progressModuloOption);
rootCommand.Add(logLevelOption);
ExtraHelpAction.AddToRootCommand(rootCommand);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
	var cachePath = parseResult.GetValue(cachePathOption)!;
	var dbPath = parseResult.GetValue(dbPathOption)!;
	var includeHttpHeaders = parseResult.GetValue(includeHttpHeadersOption);
	var includeBodyFileSignature = parseResult.GetValue(includeBodyFileSignatureOption);
	var degreeOfParallelism = parseResult.GetValue(degreeOfParallelismOption);
	var sqlBatchSize = parseResult.GetValue(sqlBatchSizeOption);
	var progressModulo = parseResult.GetValue(progressModuloOption);

	var logLevel = parseResult.GetValue(logLevelOption);

	var builder = Host.CreateApplicationBuilder();

	const int pathsChannelCapacity = 256;
	const int itemsChannelCapacity = 256;

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
		options.LogLevel = logLevel;
	});

	builder.Services.AddDbContext<NginxCacheContext>(o => o
		.UseSqlite($"DataSource={dbPath};Foreign Keys=true")
		.UseSnakeCaseNamingConvention());

	builder.Logging.ClearProviders();
	builder.Logging.SetMinimumLevel(logLevel);
	builder.Logging.AddFilter("Microsoft.*", LogLevel.Warning);
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

