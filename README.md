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

where `Duet3Firmware_MB6HC.bin` is your firmware binary.

If you want to append all subdirectories and files from a given `SD` directory, run

```
CrcAppender Duet3Firmware_MB6HC.bin ./SD
```

instead.

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

## License

This program is licensed under the terms of the [GPLv3](LICENSE).
