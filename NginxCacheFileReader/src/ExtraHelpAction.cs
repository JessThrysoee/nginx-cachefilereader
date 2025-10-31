using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

internal class ExtraHelpAction : SynchronousCommandLineAction
{
	private readonly HelpAction _defaultHelp;

	public ExtraHelpAction(HelpAction action) => _defaultHelp = action;

	public override int Invoke(ParseResult parseResult)
	{
		int result = _defaultHelp.Invoke(parseResult);

		Console.WriteLine("""
		Usage Command:

		  nginx-cachefilereader --help
		  nginx-cachefilereader --cache-path /var/cache/nginx --db-path /tmp/nginx_cache.db
		  nginx-cachefilereader --cache-path /var/cache/nginx --db-path /tmp/nginx_cache.db --sql-batch-size 10000 --progress-modulo 10000
		  nginx-cachefilereader --cache-path /var/cache/nginx --db-path /tmp/nginx_cache.db --log-level Debug

		Usage Database::

		  * Delete all cache items with status greater than or equal to 500

		    sqlite3 nginx_cache.db "select 'rm ' || path from cache_items where status_code >= 500" > rm.sh
		    bash rm.sh

		""");

		return result;

	}

	public static void AddToRootCommand(RootCommand rootCommand)
	{
		for (int i = 0; i < rootCommand.Options.Count; i++)
		{
			if (rootCommand.Options[i] is HelpOption defaultHelpOption)
			{
				defaultHelpOption.Action = new ExtraHelpAction((HelpAction)defaultHelpOption.Action!);
				break;
			}
		}
	}
}