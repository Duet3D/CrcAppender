# CRC32 Appender Utility

## Description

This program is designed to append a CRC32 checksum to a RepRapFirmware binary.
Besides, it allows the storage of SD files as part of a read-only file system so that system files can be embedded in a firmware build.

## Usage

In order to use the standalone files, the [.NET Runtime](https://dotnet.microsoft.com/download) must be installed.

To calculate and append the CRC32 checksum, just run

```
CrcAppender Duet3Firmware_MB6HC.bin
```

where `Duet3Firmware_MB6HC.bin` is your firmware binary. For further instructions about embedding SD files, see [below](#embedding-files).

## Building

To build this application the [.NET SDK](https://dotnet.microsoft.com/download/dotnet/5.0) is required. To build it on any platform, run the following command on

### Windows x64
```
dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained false
```

### Linux x64
```
dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained false
```

### OS X
```
dotnet publish -r osx-x64 -p:PublishSingleFile=true --self-contained false
```

This will generate a standalone application without additional runtime files. See [here](https://docs.microsoft.com/de-de/dotnet/core/rid-catalog) for a full list of runtime identifiers.

## Embedding files

The files to be embedded must be stored in a directory structure matching the one to be embedded. The CrcAppender program accepts an optional second parameter, which is the root of that directory structure. For example, if the embedded files are are stored in a directory tree whose root is C:\EmbeddedFiles then the following command would be run to append the files to the firmware binary and store the CRC:

```
CrcAppender RepPapFirmare.bin C:\EmbeddedFiles
```

This CrcAppender step replaces the crc32appender step in the firmware build process.

CrcAppender is a .NET program that runs under both Windows and Linux. It requires .NET Core 5 to be installed on the machine on which it is run.

## AppendCrc behaviour
The appendcrc program will append the following data to the original firmware binary. As the original binary always ends on a 4-byte boundary, this data will start on a 4-byte boundary.

```
[MAGIC (uint32_t)]
[File offset of the DIRECTORIES (uint32_t)]
[NUM_FILES (uint32_t)]
[FILE1 descriptor]
[FILE2 descriptor]
[FILE3 descriptor]
[...]
```

The MAGIC word has the value `0x543C2BEF`.

Each file descriptor comprising the following data:
- `uint32_t nameOffset` (offset to the filename) -> FILE_NAME
- `uint32_t contentOffset` (offset to the data content) -> FILE_CONTENT
- `uint32_t contentLength`

each of the following are 0-terminated but not necessarily on a 4-byte boundary:

```
[DIR_NAME1] [DIR_NAME2] [EMPTY STRING] ... [FILE_NAME1] [FILE_NAME2] ...
```

This is then followed by

```
(optional padding to remain on a 4-byte boundary)
FILE_CONTENT1 (0-terminated)

(optional padding to remain on a 4-byte boundary)
FILE_CONTENT2 (0-terminated)

… other file content …
```

Finally, AppendCrc appends the crc of the whole file including the embedded files. It also adjusts the pointer at offset 0x0C in the file to the CRC by adding to it the total amount of embedded file data it added.

## License

This program is licensed under the terms of the [GPLv3](LICENSE).
