namespace NginxCacheFileReader.Tests;

public class Tests
{
	[SetUp]
	public void Setup()
	{
	}

	[Test]
	public async Task Test1()
	{
		//var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "resources", "c51a37455db8d92f6bede15943af1382");
		//var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "resources", "ae53320b4cbec97fe244fea7d59e7a6d");
		var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "resources", "cachefile_no-body");

		Assert.That(File.Exists(path), $"Expected test data at: {path}");

		var cacheItem = await NginxCacheFile.ReadAsync(path, includeHttpHeaders: true, includeBodyFileSignature: true);

		Assert.Pass();
	}
}

