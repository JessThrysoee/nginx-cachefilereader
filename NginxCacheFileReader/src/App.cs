using System.Data;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NginxCacheFileReader;

public class App : BackgroundService
{
	private readonly ILogger<App> _logger;
	private readonly IHostApplicationLifetime _lifetime;
	private readonly IServiceProvider _serviceProvider;
	private readonly Args _args;

	public App(ILogger<App> logger, IHostApplicationLifetime lifetime, IServiceProvider serviceProvider, IOptions<Args> args)
	{
		_logger = logger;
		_lifetime = lifetime;
		_serviceProvider = serviceProvider;
		_args = args.Value;

		_logger.LogDebug("{Args}", _args);
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		try
		{
			var countFiles = CountFiles(_args.CachePath);
			_logger.LogDebug("number of cache files {Count}", countFiles);

			var pathsChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(_args.PathsChannelCapacity)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleWriter = true,
				SingleReader = false
			});

			var itemsChannel = Channel.CreateBounded<CacheItem>(new BoundedChannelOptions(_args.ItemsChannelCapacity)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleWriter = false,
				SingleReader = true
			});

			var procuceFilePathTask = Task.Run(async () =>
			{
				await ProduceFilePaths(pathsChannel, _args.CachePath, cancellationToken);
			}, cancellationToken);

			var produceCacheItemsTask = Task.Run(async () =>
			{
				await ProduceCacheItems(pathsChannel, itemsChannel, _args.DegreeOfParallelism, cancellationToken);
			}, cancellationToken);

			var writeItemToDatabaseTask = Task.Run(async () =>
			{
				await WriteItemToDatabase(itemsChannel, _args.SqlBatchSize, _args.ProgressModulo, cancellationToken);
			}, cancellationToken);

			try
			{
				await Task.WhenAll(procuceFilePathTask, produceCacheItemsTask, writeItemToDatabaseTask);
			}
			catch (OperationCanceledException)
			{
				_logger.LogDebug("Operation was cancelled.");
			}

			_logger.LogDebug("All tasks completed.");
			_lifetime.StopApplication();

		}
		catch (Exception e)
		{
			_logger.LogError(e, "ExecuteAsync failed");

		}
	}


	int CountFiles(string cachePath)
	{
		int count = 0;
		foreach (var dir in Directory.EnumerateDirectories(cachePath, "*").Select(x => new DirectoryInfo(x)))
		{
			if (dir.Name.EndsWith("_temp") || dir.Name.Equals("lost+found")) continue;

			count += Directory.EnumerateFiles(dir.FullName, "*", SearchOption.AllDirectories).Count();
		}

		return count;
	}


	async Task ProduceFilePaths(
		Channel<string> pathsChannel,
		string cachePath,
		CancellationToken cancellationToken)
	{
		try
		{
			foreach (var dir in Directory.EnumerateDirectories(cachePath, "*").Select(x => new DirectoryInfo(x)))
			{
				if (dir.Name.EndsWith("_temp") || dir.Name.Equals("lost+found")) continue;

				var paths = Directory.EnumerateFiles(dir.FullName, "*", SearchOption.AllDirectories);

				foreach (var path in paths)
				{
					cancellationToken.ThrowIfCancellationRequested();
					await pathsChannel.Writer.WriteAsync(path, cancellationToken);
				}
			}
		}
		finally
		{
			_logger.LogDebug("pathsChannel Complete");
			pathsChannel.Writer.Complete();
		}
	}

	async Task ProduceCacheItems(
		Channel<string> pathsChannel,
		Channel<CacheItem> itemsChannel,
		int degreeOfParallelism,
		CancellationToken cancellationToken)
	{
		List<Task> tasks = [];

		for (var i = 0; i < degreeOfParallelism; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				while (await pathsChannel.Reader.WaitToReadAsync(cancellationToken))
				{
					while (pathsChannel.Reader.TryRead(out var path))
					{
						cancellationToken.ThrowIfCancellationRequested();

						try
						{
							if (!File.Exists(path)) continue;

							var cacheItem = await NginxCacheFile.ReadAsync(path,
													  includeHttpHeaders: _args.IncludeHttpHeaders,
													  includeBodyFileSignature: _args.IncludeBodyFileSignature,
													  cancellationToken: cancellationToken);
							if (cacheItem is not null)
							{
								await itemsChannel.Writer.WriteAsync(cacheItem, cancellationToken);
							}

						}
						catch (Exception e) when (e is not OperationCanceledException)
						{
							_logger.LogError(e, "ProduceCacheItems failed for cache file {Path}", path);
						}
					}
				}
			}, cancellationToken));
		}

		try
		{
			await Task.WhenAll(tasks);
		}
		catch (OperationCanceledException)
		{
			_logger.LogDebug("ProduceCacheItems was cancelled.");
		}
		finally
		{
			_logger.LogDebug("itemsChannel Complete");
			itemsChannel.Writer.Complete();
		}
	}

	async Task WriteItemToDatabase(
		Channel<CacheItem> itemsChannel,
		int sqlBatchSize,
		int progressModulo,
		CancellationToken cancellationToken)
	{
		long count = 0;
		List<CacheItem> items = [];
		var stopwatch = Stopwatch.StartNew();

		await foreach (var cacheItem in itemsChannel.Reader.ReadAllAsync(cancellationToken))
		{
			cancellationToken.ThrowIfCancellationRequested();

			count += 1;
			items.Add(cacheItem);

			if (count % sqlBatchSize == 0)
			{
				await Insert(items, cancellationToken);
				items = [];
			}

			if (count % progressModulo == 0)
			{
				var elapsed = stopwatch.Elapsed;
				_logger.LogInformation("{Count} (elapsed: {Elapsed:mm\\:ss\\.fff})", count, elapsed);
				stopwatch.Restart();
			}
		}

		{
			await Insert(items, cancellationToken);
		}

		var finalElapsed = stopwatch.Elapsed;
		_logger.LogInformation("{Count} (final elapsed: {Elapsed:mm\\:ss\\.fff})", count, finalElapsed);
	}


	async Task Insert(List<CacheItem> items, CancellationToken ct)
	{
		using var scope = _serviceProvider.CreateScope();
		await using var db = scope.ServiceProvider.GetRequiredService<NginxCacheContext>();

		await db.Database.OpenConnectionAsync(ct);
		await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=OFF;", ct);
		await db.Database.ExecuteSqlRawAsync("PRAGMA synchronous=OFF;", ct);
		await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;", ct);
		await db.Database.ExecuteSqlRawAsync("PRAGMA temp_store=MEMORY;", ct);
		await db.Database.ExecuteSqlRawAsync("PRAGMA cache_size=-500000;", ct);

		db.ChangeTracker.AutoDetectChangesEnabled = false;

		await using var tx = await db.Database.BeginTransactionAsync(ct);
		await db.AddRangeAsync(items, ct);
		await db.SaveChangesAsync(ct);
		await tx.CommitAsync(ct);

		db.ChangeTracker.Clear();

		// TODO create indexes after bulk insert with EF ExecuteSqlRawAsync
		// migrationBuilder.Sql(@"
		//     CREATE INDEX ix_http_header_cache_item_id ON http_header (cache_item_id);
		//     CREATE INDEX ix_cache_item_header_cache_item_id ON cache_item_header (cache_item_id);
		// ");
	}
}
