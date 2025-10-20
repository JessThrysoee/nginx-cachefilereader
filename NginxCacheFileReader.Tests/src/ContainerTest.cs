using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace NginxCacheTests
{
	[TestFixture]
	public class NginxProxyCacheTests
	{
		private IContainer _nginx = default!;
		private string _workDir = default!;
		private string _confFile = default!;
		private string _cacheDir = default!;


		private static readonly string NginxConf = """
worker_processes  1;

events {
    worker_connections  1024;
}

http {
    proxy_cache_path /tmp/nginx_cache levels=1:2 keys_zone=my_cache:10m inactive=60s use_temp_path=off;

    server {
        listen 8080;
        server_name localhost;

        location / {
            proxy_pass http://127.0.0.1:8081;
            proxy_cache my_cache;
            proxy_cache_valid 200 1m;
            add_header X-Cache-Status $upstream_cache_status;
        }
    }

    server {
        listen 8081;
        server_name backend;

        location / {
            default_type text/plain;
            add_header Vary "Accept-Encoding, User-Agent, Accept-Language" always;
            add_header Variant "Accept-Encoding;gzip,br,identity, Accept-Language;en,fr,de, User-Agent" always;
            add_header ETag "\"static-etag-12345\"" always;
            add_header Last-Modified "Mon, 01 Jan 2024 00:00:00 GMT" always;
            return 200 "backend response\n";
        }
    }
}
""";

		[OneTimeSetUp]
		public async Task Setup()
		{
			_workDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "nginx-test-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_workDir);

			_confFile = Path.Combine(_workDir, "nginx.conf");
			await File.WriteAllTextAsync(_confFile, NginxConf);

			_cacheDir = Path.Combine(_workDir, "cache");
			Directory.CreateDirectory(_cacheDir);

			_nginx = new ContainerBuilder()
			  .WithImage("nginx:1.28-alpine3.21")
			  .WithBindMount(_confFile, "/etc/nginx/nginx.conf", AccessMode.ReadOnly)
			  .WithBindMount(_cacheDir, "/tmp/nginx_cache")
			  .WithPortBinding(8080, true) // frontend cache
			  .WithPortBinding(8081, true) // backend
			  .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8080)))
			  .Build();

			await _nginx.StartAsync();
		}

		[OneTimeTearDown]
		public async Task TearDown()
		{
			if (_nginx != null)
			{
				await _nginx.StopAsync();
				await _nginx.DisposeAsync();
			}
			try { Directory.Delete(_workDir, recursive: true); } catch { }
		}

		[Test]
		public async Task Backend_Should_Return_Headers()
		{
			var backendPort = _nginx.GetMappedPublicPort(8080);
			using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{backendPort}") };

			var resp = await http.GetAsync("/");
			var body = await resp.Content.ReadAsStringAsync();

			Assert.That(body, Does.Contain("backend response"));
			Assert.That(resp.Headers.Contains("Vary"));
			Assert.That(resp.Headers.Contains("ETag"));
		}

		[Test]
		public async Task Cache_Should_Miss_Then_Hit()
		{
			var frontendPort = _nginx.GetMappedPublicPort(8080);
			using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{frontendPort}") };

			var first = await http.GetAsync("/?x=Cache_Should_Miss_Then_Hit");
			var miss = first.Headers.Contains("X-Cache-Status")
			  ? string.Join(",", first.Headers.GetValues("X-Cache-Status"))
			  : "";
			Assert.That(miss, Is.EqualTo("MISS"));

			var second = await http.GetAsync("/?x=Cache_Should_Miss_Then_Hit");
			var hit = second.Headers.Contains("X-Cache-Status")
			  ? string.Join(",", second.Headers.GetValues("X-Cache-Status"))
			  : "";
			Assert.That(hit, Is.EqualTo("HIT"));
		}

		[Test]
		public async Task Prime_Cache_With_100_Random_Requests()
		{
			var frontendPort = _nginx.GetMappedPublicPort(8080);
			using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{frontendPort}") };
			var random = new Random();

			var cacheHits = 0;
			var cacheMisses = 0;

			// Prime cache with 100 requests with random query strings
			for (int i = 0; i < 100; i++)
			{
				// Generate random query string parameters
				var randomParam1 = $"param1={GenerateRandomString(random, 8)}";
				var randomParam2 = $"param2={random.Next(1, 1000)}";
				var randomParam3 = $"param3={GenerateRandomString(random, 5)}";
				var randomParam4 = $"id={Guid.NewGuid():N}";

				var queryString = $"?{randomParam1}&{randomParam2}&{randomParam3}&{randomParam4}";

				var response = await http.GetAsync($"/{queryString}");

				// Check cache status
				if (response.Headers.Contains("X-Cache-Status"))
				{
					var cacheStatus = string.Join(",", response.Headers.GetValues("X-Cache-Status"));
					if (cacheStatus == "HIT")
						cacheHits++;
					else if (cacheStatus == "MISS")
						cacheMisses++;
				}

				// Verify response is successful
				Assert.That(response.IsSuccessStatusCode, Is.True, $"Request {i + 1} failed");
			}

			// Verify that we got cache misses (since each request should be unique)
			Assert.That(cacheMisses, Is.GreaterThan(0), "Expected at least some cache misses with random query strings");

			// Log cache statistics
			TestContext.Out.WriteLine($"Cache statistics: {cacheHits} hits, {cacheMisses} misses out of 100 requests");
		}

		private static string GenerateRandomString(Random random, int length)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			return new string(Enumerable.Repeat(chars, length)
				.Select(s => s[random.Next(s.Length)]).ToArray());
		}
	}
}
