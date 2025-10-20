# nginx-cachefilereader

    Description:
      Nginx Cache File Reader - Read Nginx cache file info to a SQLite database

    Usage:
      nginx-cachefilereader [options]

    Options:
      --cache-path <cache-path> (REQUIRED)               Path to the Nginx cache directory
      --db-path <db-path> (REQUIRED)                     Path to the SQLite database file
      --include-http-headers                             Include HTTP headers in the database
      --include-body-file-signature                      Include body file signature (hex and ascii) in the database
      --degree-of-parallelism <degree-of-parallelism>    Number of parallel workers for processing files [default: 16]
      --paths-channel-capacity <paths-channel-capacity>  Capacity of the paths channel buffer [default: 256]
      --items-channel-capacity <items-channel-capacity>  Capacity of the items channel buffer [default: 256]
      --sql-batch-size <sql-batch-size>                  Number of items to batch before writing to database [default: 1000]
      --progress-modulo <progress-modulo>                Display progress every N items [default: 1000]
      -?, -h, --help                                     Show help and usage information
      --version                                          Show version information

    Usage Command:

      nginx-cachefilereader --help
      nginx-cachefilereader --cache-path /var/cache/nginx --db-path /tmp/nginx_cache.db
      nginx-cachefilereader --cache-path /var/cache/nginx --db-path /tmp/nginx_cache.db --sql-batch-size 10000 --progress-modulo 10000

    Usage Database::

      * Delete all cache items with status greater than or equal to 500

        sqlite3 nginx_cache.db "select 'rm ' || path from cache_items where status_code >= 500" > rm.sh
        bash rm.sh








    SELECT * FROM cache_items ci
      JOIN cache_item_header cih ON cih.cache_item_id = ci.cache_item_id
      JOIN http_header hh        ON hh.cache_item_id  = ci.cache_item_id
    WHERE 
      ci.cache_item_id = 7;




    SELECT * FROM cache_items ci
      JOIN cache_item_header cih ON cih.cache_item_id = ci.cache_item_id
      JOIN http_header hh        ON hh.cache_item_id  = ci.cache_item_id
    WHERE 
      ci.cache_item_id = 7;



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








## ----------------------------------------------------------



    select ci.cache_item_id, ci.status_code, ci.path, h.*, cih.* from cache_items ci
       join cache_item_header cih on ci.cache_item_id = cih.cache_item_id
       join http_header h on ci.cache_item_id = h.cache_item_id
       where ci.cache_item_id = 52;

       http_header



    select ci.cache_item_id, ci.status_code, ci.path, h1.value as 'content_type', h2.value as 'x-aar', cih.* from cache_items ci
       join cache_item_header cih on ci.cache_item_id = cih.cache_item_id
       join http_header h1 on ci.cache_item_id = h1.cache_item_id
       join http_header h2 on ci.cache_item_id = h2.cache_item_id
       where ci.cache_item_id = 52  and h1.name = 'Content-Type' and h2.name = 'X-Correlation-Id';




    select * from http_header where cache_item_id = 52





    .mode box


    SELECT
    COUNT(*) AS COUNT_TOTAL,
        SUM(case when key like '%access_token%' then 1 else 0 end) as COUNT_ACCESS_TOKEN,
        ROUND(AVG(case when key like '%access_token%' then 1.0 else 0 end) * 100.0) || '%'  as PERCENT_ACCESS_TOKEN,
        SUM(case when status_code NOT BETWEEN 200 AND 299 then 1 else 0 end) as COUNT_NOT_2XX
        --ROUND(AVG(case when status_code NOT BETWEEN 200 AND 299 then 1.0 else 0 end)) as PERCENT_NOT_OK
    FROM cache_items;



    SELECT COUNT(*) FROM cache_items;



