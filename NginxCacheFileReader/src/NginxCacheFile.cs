using System.Buffers;
using System.Text;
using Microsoft.IO;
using Microsoft.Win32.SafeHandles;

namespace NginxCacheFileReader;

// https://github.com/nginx/nginx/blob/master/src/http/ngx_http_cache.h
// https://github.com/nginx/nginx/blob/master/src/http/ngx_http_file_cache.c

public class NginxCacheFile
{
	private static readonly RecyclableMemoryStreamManager _memoryStreamManager = new();
	private static readonly ArrayPool<byte> _bytesArrayPool = ArrayPool<byte>.Shared;

	public static async Task<CacheItem?> ReadAsync(
		string path,
		bool includeHttpHeaders = false,
		bool includeBodyFileSignature = false,
		CancellationToken cancellationToken = default)
	{
		SafeFileHandle? fileHandle = null;
		try
		{
			fileHandle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous | FileOptions.RandomAccess);
			var fileLength = RandomAccess.GetLength(fileHandle);

			const int pageSize = 4096;
			const int bytesArraySize = 65536;

			var bytes = _bytesArrayPool.Rent(bytesArraySize);
			try
			{
				var bytesRead = await RandomAccess.ReadAsync(fileHandle, bytes.AsMemory(0, pageSize), fileOffset: 0, cancellationToken);
				if (bytesRead < NginxCacheHeader.Size)
				{
					return null; // File probably deleted by nginx or corrupted
				}

				var nginxHeader = ReadBinaryHeader(bytes);

				if (bytesRead < fileLength)
				{
					await ReadMoreBytes(fileHandle, nginxHeader, bytes, bytesArraySize, bytesRead, cancellationToken);
				}

				if (fileHandle != null && !fileHandle.IsInvalid && !fileHandle.IsClosed)
				{
					fileHandle.Dispose();
					fileHandle = null;
				}

				var key = ReadKey(nginxHeader, bytes);
				var (statusCode, _) = ReadHttpStatusLine(nginxHeader, bytes);

				List<HttpHeader> httpHeaders = [];
				string? bodyFileSignatureHex = null;
				string? bodyFileSignatureAscii = null;

				if (includeHttpHeaders)
				{
					httpHeaders = ReadHttpHeaders(nginxHeader, bytes);

					if (includeBodyFileSignature && nginxHeader.BodyStart < fileLength)
					{
						(bodyFileSignatureHex, bodyFileSignatureAscii) = ReadBodyFileSignature(nginxHeader, bytes);
					}
				}

				var cacheItem = new CacheItem
				{
					Key = key,
					StatusCode = statusCode,
					Path = path,
					HttpHeaders = httpHeaders,
					BodyFileSignatureHex = bodyFileSignatureHex,
					BodyFileSignatureAscii = bodyFileSignatureAscii,
					CacheItemHeader = new CacheItemHeader(nginxHeader)
				};
				return cacheItem;
			}
			finally
			{
				_bytesArrayPool.Return(bytes);
			}
		}
		finally
		{
			if (fileHandle != null && !fileHandle.IsInvalid && !fileHandle.IsClosed)
			{
				fileHandle.Dispose();
			}
		}
	}


	private static NginxCacheHeader ReadBinaryHeader(byte[] bytes)
	{
		using var memoryStream = _memoryStreamManager.GetStream("NginxBinaryHeader", bytes, 0, NginxCacheHeader.Size);
		using var br = new BinaryReader(memoryStream);

		var version = NginxCacheHeader.GetVersion(br);

		var nginxHeader = new NginxCacheHeader
		{
			Version = version,
			ValidSec = br.ReadInt64(),
			UpdatingSec = br.ReadInt64(),
			ErrorSec = br.ReadInt64(),
			LastModified = br.ReadInt64(),
			Date = br.ReadInt64(),
			Crc32 = br.ReadUInt32(),
			ValidMsec = br.ReadUInt16(),
			HeaderStart = br.ReadUInt16(),
			BodyStart = br.ReadUInt16(),
			EtagLen = br.ReadByte(),
			Etag = br.ReadBytes(128),
			VaryLen = br.ReadByte(),
			Vary = br.ReadBytes(128),
			Variant = br.ReadBytes(16),
		};

		if (memoryStream.Position != NginxCacheHeader.Size)
		{
			throw new NginxCacheFileException($"Position={memoryStream.Position}, Expected {NginxCacheHeader.Size}");
		}

		return nginxHeader;
	}


	private static async Task ReadMoreBytes(SafeFileHandle fileHandle, NginxCacheHeader nginxHeader, byte[] bytes, int bytesArraySize, int bytesRead, CancellationToken cancellationToken)
	{
		var requiredBytes = nginxHeader.BodyStart + NginxCacheHeader.BodyFileSignatureSize;
		if (bytesRead >= requiredBytes)
		{
			return; // We already have enough bytes
		}
		if (bytesArraySize < requiredBytes)
		{
			throw new NginxCacheFileException($"Buffer size {bytesArraySize} is smaller than required bytes {requiredBytes}");
		}

		var additionalBytesNeeded = requiredBytes - bytesRead;
		var additionalBytesRead = await RandomAccess.ReadAsync(fileHandle, bytes.AsMemory(bytesRead, additionalBytesNeeded), fileOffset: bytesRead, cancellationToken);
		if (additionalBytesRead < additionalBytesNeeded)
		{
			throw new NginxCacheFileException($"Could not read all required bytes. Needed {additionalBytesNeeded}, read {additionalBytesRead}");
		}
	}


	private static string ReadKey(NginxCacheHeader nginxHeader, Span<byte> bytes)
	{
		var start = NginxCacheHeader.Size + NginxCacheHeader.Padding + NginxCacheHeader.KeyLabelSize;
		var length = nginxHeader.HeaderStart - start - NginxCacheHeader.Lf;
		var key = Encoding.UTF8.GetString(bytes.Slice(start: start, length: length));
		return key;
	}


	private static (int statusCode, string statusLine) ReadHttpStatusLine(NginxCacheHeader nginxHeader, Span<byte> bytes)
	{
		var statusLine = GetHeaderSection(nginxHeader, bytes, HeaderSectionPart.StatusLine);

		var statusCode = -1;
		if (statusLine.StartsWith("HTTP/"))
		{
			statusCode = int.Parse(statusLine.Split()[1]);
		}
		return (statusCode, statusLine);
	}


	private static List<HttpHeader> ReadHttpHeaders(NginxCacheHeader nginxHeader, Span<byte> bytes)
	{
		var httpHeadersStr = GetHeaderSection(nginxHeader, bytes, HeaderSectionPart.Headers);

		List<HttpHeader> httpHeaders = [];

		foreach (var headerLine in httpHeadersStr.Split(["\r\n"], StringSplitOptions.None))
		{
			var a = headerLine.Split([": "], 2, StringSplitOptions.None);
			if (a.Length > 1 && !string.IsNullOrEmpty(a[0]) && !string.IsNullOrEmpty(a[1]))
			{
				httpHeaders.Add(new HttpHeader { Name = a[0], Value = a[1] });
			}
		}

		return httpHeaders;
	}


	enum HeaderSectionPart { StatusLine, Headers }

	private static string GetHeaderSection(NginxCacheHeader nginxHeader, Span<byte> bytes, HeaderSectionPart part)
	{
		var start = nginxHeader.HeaderStart;
		var length = nginxHeader.BodyStart - nginxHeader.HeaderStart - (2 * NginxCacheHeader.Crlf);
		var headerSection = bytes[start..(start + length)];
		var crlf = headerSection.IndexOf("\r\n"u8);

		return part switch
		{
			HeaderSectionPart.StatusLine => Encoding.UTF8.GetString(headerSection[..crlf]),
			HeaderSectionPart.Headers => Encoding.UTF8.GetString(headerSection[crlf..]),
			_ => throw new NginxCacheFileException($"Unexpected HeaderSectionPart: {nameof(part)}"),
		};
	}


	private static (string bodyFileSignatureHex, string bodyFileSignatureAscii) ReadBodyFileSignature(NginxCacheHeader nginxHeader, Span<byte> bytes)
	{
		var bodyStart = bytes.Slice(nginxHeader.BodyStart, NginxCacheHeader.BodyFileSignatureSize);
		var bodyFileSignatureHex = Convert.ToHexString(bodyStart); // https://en.wikipedia.org/wiki/List_of_file_signatures
		var bodyFileSignatureAscii = string.Concat(bodyStart.ToArray().Select(x => x >= 32 && x <= 128 ? (char)x : '.')); // like xxd

		return (bodyFileSignatureHex, bodyFileSignatureAscii);
	}
}


// type sizes for x86_64 GNU/Linux
public record class NginxCacheHeader()
{
	private const int NGX_HTTP_CACHE_VERSION = 5;

	public ulong Version { get; init; }
	public long ValidSec { get; init; }
	public long UpdatingSec { get; init; }
	public long ErrorSec { get; init; }
	public long LastModified { get; init; }
	public long Date { get; init; }
	public uint Crc32 { get; init; }
	public ushort ValidMsec { get; init; }
	public ushort HeaderStart { get; init; }
	public ushort BodyStart { get; init; }
	public byte EtagLen { get; init; }
	public required byte[] Etag { get; init; }
	public byte VaryLen { get; init; }
	public required byte[] Vary { get; init; }
	public required byte[] Variant { get; init; }

	public static int Size = 332; // Sum of field type sizes = 8 + (5 * 8) + 4 + (3 * 2) + 1 + 128 + 1 + 128 + 16 = 332
	public static int StructSize = 336;  // sizeof(ngx_http_file_cache_header_t)
	public static int Padding = 4;  // struct is padded to an 8-byte boundary, alignof(ngx_http_file_cache_header_t) = 8 (332 + 4 % 8 == 0)

	public static int Lf = 1; // '\n'
	public static int Crlf = 2; // '\r\n'
	public static int KeyLabelSize = 6; // '\nKEY: '
	public static int BodyFileSignatureSize = 15;

	public static ulong GetVersion(BinaryReader br)
	{
		var version = br.ReadUInt64();
		if (version != NGX_HTTP_CACHE_VERSION) throw new NginxCacheFileException($"nginx cache file version != {NGX_HTTP_CACHE_VERSION}");
		return version;
	}
}

public class NginxCacheFileException(string message) : Exception(message) { }
