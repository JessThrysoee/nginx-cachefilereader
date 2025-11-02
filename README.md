# nginx-cachefilereader

    Description:
      Nginx Cache File Reader - Read Nginx cache file info to a SQLite database

    Usage:
      nginx-cachefilereader [options]

    Options:
      --cache-path <cache-path> (REQUIRED)                               Path to the Nginx cache directory
      --db-path <db-path> (REQUIRED)                                     Path to the SQLite database file
      --include-http-headers                                             Include HTTP headers in the database
      --include-body-file-signature                                      Include body file signature (hex and ascii) in the database
      --degree-of-parallelism <degree-of-parallelism>                    Number of parallel workers for processing files [default: 16]
      --sql-batch-size <sql-batch-size>                                  Number of items to batch before writing to database [default: 10000]
      --progress-modulo <progress-modulo>                                Display progress every N items [default: 10000]
      --log-level <Critical|Debug|Error|Information|None|Trace|Warning>  Logging level (Trace|Debug|Information|Warning|Error|Critical|None) [default: Information]
      -?, -h, --help                                                     Show help and usage information
      --version                                                          Show version information

# Schema

    CREATE TABLE IF NOT EXISTS "cache_items" (
        "cache_item_id" INTEGER NOT NULL CONSTRAINT "pk_cache_items" PRIMARY KEY AUTOINCREMENT,
        "key" TEXT NOT NULL,
        "status_code" INTEGER NOT NULL,
        "path" TEXT NOT NULL,
        "body_file_signature_hex" TEXT NULL,
        "body_file_signature_ascii" TEXT NULL
    );

    CREATE TABLE IF NOT EXISTS "cache_item_header" (
        "cache_item_header_id" INTEGER NOT NULL CONSTRAINT "pk_cache_item_header" PRIMARY KEY AUTOINCREMENT,
        "version" INTEGER NOT NULL,
        "valid_sec" TEXT NULL,
        "updating_sec" TEXT NULL,
        "error_sec" TEXT NULL,
        "last_modified" TEXT NULL,
        "date" TEXT NULL,
        "crc32" INTEGER NULL,
        "valid_msec" INTEGER NULL,
        "etag" TEXT NULL,
        "vary" TEXT NULL,
        "variant" TEXT NULL,
        "cache_item_id" INTEGER NOT NULL,
        CONSTRAINT "fk_cache_item_header_cache_items_cache_item_id" FOREIGN KEY ("cache_item_id") REFERENCES "cache_items" ("cache_item_id") ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS "http_header" (
        "http_header_id" INTEGER NOT NULL CONSTRAINT "pk_http_header" PRIMARY KEY AUTOINCREMENT,
        "name" TEXT NOT NULL,
        "value" TEXT NOT NULL,
        "cache_item_id" INTEGER NOT NULL,
        CONSTRAINT "fk_http_header_cache_items_cache_item_id" FOREIGN KEY ("cache_item_id") REFERENCES "cache_items" ("cache_item_id") ON DELETE CASCADE
    );
