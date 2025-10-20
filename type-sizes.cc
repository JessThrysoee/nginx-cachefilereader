#include <iostream> // cout
#include <cstdint>
#include <ctime>    // time_t
#include <typeinfo> // typeid, name


// clang++ -std=c++1y type-sizes.cc


using namespace std;


//typedef unsigned char   u_char;
//typedef unsigned short  u_short;
//

#define NGX_HTTP_CACHE_KEY_LEN       16
#define NGX_HTTP_CACHE_ETAG_LEN      128
#define NGX_HTTP_CACHE_VARY_LEN      128

#define SHOW(expr) std::cout << #expr << " = " << (expr) << '\n'

int main()
{

    cout << "sizeof(long): \t\t"
         << sizeof(long) << endl;

    cout << "sizeof(unsigned long): \t\t"
         << sizeof(unsigned long) << endl;

    cout << "sizeof(time_t): \t\t"
         << sizeof(time_t) << endl;
    
    cout << "sizeof(unsigned int): \t\t"
         << sizeof(unsigned int) << endl;

    cout << "sizeof(unsigned short): \t\t"
         << sizeof(unsigned short) << endl;

    cout << "sizeof(unsigned char): \t\t"
         << sizeof(unsigned char) << endl;

    cout << "sizeof(uintptr_t): \t\t"
         << sizeof(uintptr_t) << endl;

    cout << "sizeof(uint32_t): \t\t"
         << sizeof(uint32_t) << endl;

    cout << "sizeof(u_short): \t\t"
         << sizeof(u_short) << endl;

    cout << "sizeof(u_char): \t\t"
         << sizeof(u_char) << endl;

    cout << "sizeof(u_char[NGX_HTTP_CACHE_ETAG_LEN]): \t\t"
         << sizeof(u_char[NGX_HTTP_CACHE_ETAG_LEN]) << endl;


    unsigned char  ngx_http_file_cache_key[] = { '\n', 'K', 'E', 'Y', ':', ' ' };

    cout << "sizeof(ngx_http_file_cache_key): \t\t"
         << sizeof(ngx_http_file_cache_key) << endl;

    //SHOW(alignof(int*));
    //SHOW(alignof(u_long));

//sizeof(long): 		8
//sizeof(unsigned long): 		8
//sizeof(unsigned int): 		4
//sizeof(unsigned short): 		2
//sizeof(unsigned char): 		1
//sizeof(u_short): 		2
//sizeof(u_char): 		1
//sizeof(u_char[NGX_HTTP_CACHE_ETAG_LEN]): 		128
//sizeof(ngx_http_file_cache_key): 		6
//sizeof(ngx_http_file_cache_header_t): 		336
//sizeof(all): 		332
//
//sizeof(uintptr_t): 		8
//sizeof(time_t): 		8
//sizeof(uint32_t): 		4
//sizeof(u_short): 		2


    typedef struct {
        uintptr_t                       version;
        time_t                           valid_sec;
        time_t                           updating_sec;
        time_t                           error_sec;
        time_t                           last_modified;
        time_t                           date;
        uint32_t                         crc32;
        u_short                          valid_msec;
        u_short                          header_start;
        u_short                          body_start;
        u_char                           etag_len;
        u_char                           etag[NGX_HTTP_CACHE_ETAG_LEN];
        u_char                           vary_len;
        u_char                           vary[NGX_HTTP_CACHE_VARY_LEN];
        u_char                           variant[NGX_HTTP_CACHE_KEY_LEN];
    } ngx_http_file_cache_header_t;

    cout << "alignof(ngx_http_file_cache_header_t): \t\t"
         << alignof(ngx_http_file_cache_header_t) << endl;

    cout << "sizeof(ngx_http_file_cache_header_t): \t\t"
         << sizeof(ngx_http_file_cache_header_t) << endl;


    cout << "sizeof(all): \t\t"
         << sizeof(uintptr_t) + sizeof(time_t) + sizeof(time_t) + sizeof(time_t) + sizeof(time_t) + sizeof(time_t) + sizeof(uint32_t) + sizeof(u_short) + sizeof(u_short) + sizeof(u_short) + sizeof(u_char) + sizeof(u_char[NGX_HTTP_CACHE_ETAG_LEN]) + sizeof(u_char) + sizeof(u_char[NGX_HTTP_CACHE_VARY_LEN]) + sizeof(u_char[NGX_HTTP_CACHE_KEY_LEN]) << endl;


    return 0;
}
