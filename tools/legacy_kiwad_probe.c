/* Exercise reviewed libkiwad reads with a constructed entry, not its unsafe
   archive initializer (unterminated magic string identified during review). */
#include <kiwad/kiwad.h>
int main(void) {
    wad_archive archive = {0};
    archive.file = tmpfile();
    if (!archive.file) return 2;
    fwrite("hello", 1, 5, archive.file);
    fflush(archive.file);
    wad_entry entry = {0};
    entry.size = 5;
    unsigned char *data = NULL;
    int plain = wad_archive_read(&archive, &entry, &data) == WE_SUCCESS
        && data != NULL && memcmp(data, "hello", 5) == 0;
    free(data);
    entry.zipped = 1;
    int unsupported = wad_archive_read(&archive, &entry, &data) == WE_UNIMPLEMENTED;
    fclose(archive.file);
    printf("{\"plain_entry_read\":%s,\"compressed_reports_unimplemented\":%s,\"initializer_exercised\":false}\n",
        plain ? "true" : "false", unsupported ? "true" : "false");
    return plain && unsupported ? 0 : 1;
}
