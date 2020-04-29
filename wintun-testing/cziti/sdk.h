#include <stdio.h>
#include <stdlib.h>
#define USING_ZITI_SHARED
#include <nf/ziti.h>
#include <nf/ziti_log.h>
#include <uv.h>


typedef struct cziti_ctx_s {
    nf_options opts;
    nf_context nf;
    uv_async_t async;
} cziti_ctx;

typedef struct libuv_ctx_s {
    uv_loop_t *l;
    uv_thread_t t;
    uv_async_t stopper;
} libuv_ctx;

void libuv_stopper(uv_async_t *a);
void libuv_init(libuv_ctx *lctx);
void libuv_runner(void *arg);
void libuv_run(libuv_ctx *lctx);
void libuv_stop(libuv_ctx *lctx);

void setLogOut(intptr_t h);
void setLogLevel(int level);

extern const char** all_configs;