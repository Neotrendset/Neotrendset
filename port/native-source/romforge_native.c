/* RomForge-Mac portability bridge, 2026-10-18. See repository LICENSE (GPL-2.0).
 * Darwin arm64 variadic arguments must be marshalled by the C compiler. */
#include <stdint.h>
#include <sys/ioctl.h>
#include <sys/disk.h>
int romforge_block_size(int fd, uint32_t *value) { return ioctl(fd, DKIOCGETBLOCKSIZE, value); }
int romforge_block_count(int fd, uint64_t *value) { return ioctl(fd, DKIOCGETBLOCKCOUNT, value); }
int romforge_bytes_available(int fd, uint32_t *value) { return ioctl(fd, FIONREAD, value); }
