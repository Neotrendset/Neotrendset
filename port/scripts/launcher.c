#include <mach-o/dyld.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
int main(int argc, char **argv) {
 char path[PATH_MAX], resolved[PATH_MAX]; uint32_t size=sizeof(path);
 if(_NSGetExecutablePath(path,&size)!=0||realpath(path,resolved)==NULL)return 1;
 char *slash=strrchr(resolved,'/');if(!slash)return 1;*slash=0;
 char payload[PATH_MAX];if(snprintf(payload,sizeof(payload),"%s/../Resources/payload/RomForge-Mac",resolved)>=sizeof(payload))return 1;
 argv[0]=payload;execv(payload,argv);perror("RomForge-Mac launch");return 1;
}
