# Apache TSFile C# Library

A production-ready C# implementation of the Apache TSFile time series file format library, providing efficient storage and retrieval of time series data.

## 📊 Status

**版本**: 1.2.0（生产就绪）  
**测试**: 195 通过 / 1 跳过 / 0 失败  
**Java 兼容性**: V3 + V4 格式完整读写，450+ 互操作文件验证通过  

详见 **[STATUS.md](STATUS.md)** 了解完整实现状态。

### V4 格式支持

- ✅ **表模型**: TAG/FIELD 列，多设备写入/查询
- ✅ **树模型**: 对齐 + 非对齐时间序列，按设备路径查询
- ✅ **Java 互操作**: 360 编码/压缩组合 + 90 表模型文件 + 综合互操作测试

## ✨ Features

- **Full Data Type Support**: All 13 Java data types (100% compatibility)
- **14/15 Encodings**: Plain, RLE, Gorilla, GorillaV1, ZigZag, Dictionary, TS_2DIFF, Diff, Bitmap, Regular, CHIMP, SPRINTZ, RLBE, Freq
- **Production Compression**: LZ4, ZSTD, Snappy, GZIP, Uncompressed (LZMA2 read-only)
- **V4 Table + Tree Model**: TAG/FIELD columns, aligned/non-aligned timeseries
- **Binary Compatible**: 450+ Java interop files validated
- **Batch Operations**: Efficient Tablet API for high throughput
- **Cross-Platform**: Windows, Linux, macOS (.NET 9/10)
- **Comprehensive Testing**: 196 tests, 99.5% pass rate

## Installation

### Using .NET CLI

```bash
cd csharp/src/Apache.TsFile
dotnet build
```

### NuGet Package (Future)

```bash
dotnet add package Apache.TsFile
```

## Quick Start

### Writing Data

```csharp
using Apache.TsFile;
using Apache.TsFile.Enums;
using Apache.TsFile.IO;
using Apache.TsFile.Schema;

// Create writer
using var writer = new TsFileWriter("data.tsfile");

// Define schema
var measurements = new List<MeasurementSchema>
{
    new MeasurementSchema("temperature", TsDataType.Float, TsEncoding.Plain, CompressionType.Gzip),
    new MeasurementSchema("humidity", TsDataType.Int32, TsEncoding.Plain, CompressionType.Snappy)
};

// Register device
writer.RegisterDevice("sensor_1", measurements);

// Create tablet for batch writing
var tablet = new Tablet("sensor_1", measurements, maxRowCount: 1024);

// Add data
tablet.AddRow(timestamp: 1000L, values: new object[] { 25.5f, 60 });
tablet.AddRow(timestamp: 1001L, values: new object[] { 26.0f, 61 });

// Write to file
writer.Write(tablet);
writer.Close();
```

### Reading Data

```csharp
using Apache.TsFile.IO;

// Open file
using var reader = new TsFileReader("data.tsfile");

// Query data
var result = reader.Query("sensor_1");

// Access timestamps and values
foreach (var timestamp in result.Timestamps)
{
    Console.WriteLine($"Timestamp: {timestamp}");
}

// Access measurement data
var temperatures = result.MeasurementData["temperature"];
var humidities = result.MeasurementData["humidity"];
```

## API Overview

### Core Classes

#### TsFileWriter

Writer for creating TSFile format files.

```csharp
public class TsFileWriter : IDisposable
{
    public TsFileWriter(string filePath);
    public void RegisterDevice(string deviceName, List<MeasurementSchema> measurements);
    public void RegisterTableSchema(TableSchema schema);
    public void Write(Tablet tablet);
    public void WriteRow(string deviceName, long timestamp, params object[] values);
    public void Close();
}
```

#### TsFileReader

Reader for reading TSFile format files.

```csharp
public class TsFileReader : IDisposable
{
    public TsFileReader(string filePath);
    public IReadOnlyDictionary<string, TableSchema> Schemas { get; }
    public QueryResult Query(string deviceName, string[]? measurements = null, 
        long? startTime = null, long? endTime = null);
    public Tablet QueryAll(string deviceName);
}
```

#### Tablet

Batch data container in columnar format.

```csharp
public class Tablet
{
    public Tablet(string deviceName, List<MeasurementSchema> schemas, int maxRowCount = 1024);
    public void AddRow(long timestamp, params object[] values);
    public void Reset();
    public object? GetValue(int column, int row);
}
```

#### MeasurementSchema

Defines schema for a single measurement.

```csharp
public class MeasurementSchema
{
    public MeasurementSchema(string measurementName, TsDataType dataType, 
        TsEncoding encoding = TsEncoding.Plain, 
        CompressionType compression = CompressionType.Uncompressed);
    
    public string MeasurementName { get; set; }
    public TsDataType DataType { get; set; }
    public TsEncoding Encoding { get; set; }
    public CompressionType Compression { get; set; }
}
```

### Enums

#### TsDataType

```csharp
public enum TsDataType : byte
{
    Boolean = 0,
    Int32 = 1,
    Int64 = 2,
    Float = 3,
    Double = 4,
    Text = 5,
    Timestamp = 8,
    String = 11
}
```

#### TsEncoding

```csharp
public enum TsEncoding : byte
{
    Plain = 0,
    Dictionary = 1,
    Rle = 2,
    Ts2Diff = 4,
    Gorilla = 8,
    ZigZag = 9,
    // ... and more
}
```

#### CompressionType

```csharp
public enum CompressionType : byte
{
    Uncompressed = 0,
    Snappy = 1,
    Gzip = 2,
    Lz4 = 7,
    Zstd = 8,
    Lzma2 = 9
}
```

## Compression Support

All major compression types are implemented and working:

| Compression | Library | Status | Platform Support |
|-------------|---------|--------|------------------|
| Uncompressed | - | ✅ Fully implemented | All |
| GZIP | System.IO.Compression | ✅ Fully implemented | All |
| **Snappy** | **IronSnappy (Pure C#)** | ✅ **Cross-platform** | **All** ✨ |
| LZ4 | K4os.Compression.LZ4 | ✅ Fully implemented | All |
| ZSTD | ZstdSharp.Port | ✅ Fully implemented | All |
| LZMA2 | - | ❌ Not implemented | - |

**✨ Recent Improvement**: Replaced Snappy.NET with IronSnappy for pure C# implementation. Snappy now works on Linux/macOS without native dependencies!

**Recommended for Production**: Use **LZ4** (fastest) or **ZSTD** (best compression ratio). **Snappy** is now also a great cross-platform option.

## Encoding Support

All 14 production encodings are implemented:

- **Plain**: Default encoding for all data types ✅
- **RLE**: Run-length encoding for Boolean, Int32, Int64 ✅
- **Gorilla**: XOR-based compression for Float, Double, Int32 ✅
- **GorillaV1**: Legacy Gorilla for Float, Double ✅
- **ZigZag**: Variable-length integer encoding for Int32, Int64 ✅
- **Dictionary**: Dictionary encoding for Text, String ✅
- **TS_2DIFF**: Two-differential for Int32, Int64, Float, Double ✅
- **Diff**: First-order delta for Int32, Int64 ✅
- **Bitmap**: Bit packing for Boolean ✅
- **Regular**: Regular interval encoding for Int64 ✅
- **CHIMP**: Advanced XOR compression for Float, Double ✅
- **SPRINTZ**: Sensor-optimized compression ✅
- **RLBE**: Run-length byte encoding ✅
- **CAMEL**: Not implemented (low priority, Double-only) ❌

## Examples

See the [examples](examples/) directory for complete working examples:

- [Basic Example](examples/BasicExample/README.md) - Simple read/write operations

## Building

```bash
# Build library
cd csharp/src/Apache.TsFile
dotnet build

# Build and run tests
cd csharp/tests/Apache.TsFile.Tests
dotnet test

# Run examples
cd csharp/examples/BasicExample
dotnet run
```

## Testing

```bash
cd csharp/tests/Apache.TsFile.Tests
dotnet test --verbosity normal
```

## Compatibility

This C# implementation is designed to be binary-compatible with the Java implementation of TSFile. Files written by the C# library can be read by Java applications and vice versa.

### Tested Compatibility

- ✅ V3 format (C# simplified + Java V3)
- ✅ V4 tree model (aligned + non-aligned, per-device query)
- ✅ V4 table model (TAG/FIELD columns, multi-device)
- ✅ All 13 data types
- ✅ 14/15 encodings (all except CAMEL)
- ✅ 5/6 compressions (LZMA2 read-only)
- ✅ 360 encoding/compression combination files
- ✅ 90 table model files
- ✅ Bidirectional: C# → Java and Java → C#

## Architecture

```
Apache.TsFile/
├── Enums/              - Data types, encoding, compression enums
├── Schema/             - MeasurementSchema, TableSchema
├── Compress/           - Compression implementations
├── Encoding/           - Encoder/Decoder implementations
├── IO/                 - TsFileWriter, TsFileReader
├── Common/             - Constants and utilities
└── Tablet.cs           - Batch data container
```

## Performance Tips

1. **Batch Writing**: Use `Tablet` for batch writes instead of `WriteRow()` for better performance
2. **Compression**: Choose appropriate compression based on your needs:
   - Snappy: Fast compression, moderate ratio
   - GZIP: Slower, better compression ratio
   - LZ4: Very fast, good for real-time scenarios
   - ZSTD: Good balance of speed and compression
3. **Tablet Size**: Use appropriate `maxRowCount` (default 1024) based on your memory constraints

## Limitations

- CAMEL encoding not implemented (low priority)
- LZMA2 compression is read-only
- Gorilla Int64 encoder has known issue (decoder works)
- Async I/O operations not yet implemented
- No advanced query features (filters, aggregations)

## Performance Benchmarks

A comprehensive benchmark tool is available to measure performance metrics:

```bash
cd benchmarks/Apache.TsFile.Benchmarks
dotnet run --configuration Release
```

### Quick Test

```bash
dotnet run --configuration Release -- --tables 10 --devices 10 --measurements 10
```

### Measured Metrics
- Registration time (device/measurement setup)
- Write time (data insertion)
- Close time (file finalization)
- Query time (read performance)
- File size (compression effectiveness)
- Memory usage (peak consumption)

## Documentation

Comprehensive documentation is available:

| Document | Description | Size |
|----------|-------------|------|
| **[STATUS.md](STATUS.md)** | Implementation status, Java comparison, production readiness | 400+ lines |
| **[USER_MANUAL.md](USER_MANUAL.md)** | Complete user guide with examples | 1,000+ lines |
| **[DESIGN.md](DESIGN.md)** | Architecture and design decisions | 600+ lines |
| **[BENCHMARKS.md](BENCHMARKS.md)** | Performance analysis and benchmark tool | 550+ lines |
| **[ROADMAP.md](ROADMAP.md)** | Project roadmap and future plans | 250+ lines |
| **[ENCODING_GUIDE.md](ENCODING_GUIDE.md)** | Guide for implementing encodings | 400+ lines |

**Quick Links**:
- 🚀 [Quick Start](#quick-start) - Get started in 5 minutes
- 📊 [Status Report](STATUS.md) - Implementation completeness
- 📖 [User Manual](USER_MANUAL.md) - Comprehensive guide
- ⚡ [Benchmarks](BENCHMARKS.md) - Performance testing

## Contributing

Contributions are welcome! Please ensure:

1. All tests pass
2. Code follows C# naming conventions
3. XML documentation for public APIs
4. Compatibility with Java implementation maintained

See [ROADMAP.md](ROADMAP.md) for areas needing help.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](../LICENSE) file.

## Resources

- [Apache IoTDB](https://iotdb.apache.org/)
- [TSFile Format Specification](https://iotdb.apache.org/UserGuide/Master/API/Programming-TsFile-API.html)
- [Java Implementation](https://github.com/apache/tsfile)

## Support

For issues and questions:

- GitHub Issues: https://github.com/apache/tsfile/issues
- Mailing List: dev@iotdb.apache.org
