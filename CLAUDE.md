# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Apache TSFile is a columnar storage file format for time series data. This repository contains multi-language implementations:

- **Java** (`java/tsfile/`) - Reference implementation (751 source files)
- **C#** (`csharp/`) - .NET 10 port (primary development focus)
- C++ (`cpp/`) and Python (`python/`) implementations also exist

The C# implementation is a port of the Java reference, designed for binary compatibility with Java-generated files.

## Build Commands

### C# (.NET 10)

```bash
# Build library
dotnet build csharp/src/Apache.TsFile/Apache.TsFile.csproj

# Build entire solution
dotnet build csharp/Apache.TsFile.slnx

# Run unit tests
dotnet test csharp/tests/Apache.TsFile.Tests/Apache.TsFile.Tests.csproj

# Run single test
dotnet test csharp/tests/Apache.TsFile.Tests/Apache.TsFile.Tests.csproj --filter "FullyQualifiedName~TestClassName.TestMethodName"

# Run benchmarks
dotnet run --project csharp/benchmarks/Apache.TsFile.Benchmarks/Apache.TsFile.Benchmarks.csproj --configuration Release
```

### Java (Maven)

```bash
# Build (skip tests)
mvn clean install -DskipTests -f java/pom.xml

# Run tests
mvn test -f java/pom.xml

# Generate interop test files
cd java/interop-tests && mvn clean package && java -jar target/interop-tests-1.0-SNAPSHOT-jar-with-dependencies.jar
```

### Interoperability Tests

```bash
# Run full Java-C# interop test suite
./run-interop-tests.sh
```

## C# Architecture

```
csharp/src/Apache.TsFile/
├── IO/           # TsFileWriter, TsFileReader (unified API for V3/V4)
├── Encoding/     # Encoder/Decoder implementations (Plain, RLE, Gorilla, ZigZag, Dictionary, TS_2DIFF, etc.)
├── Compress/     # Compressor implementations (GZIP, LZ4, ZSTD, Snappy)
├── Schema/       # MeasurementSchema, TableSchema, ColumnSchema
├── Enums/        # TsDataType, TsEncoding, CompressionType, ColumnCategory
├── Common/       # Constants and utilities
└── Tablet.cs     # Batch data container for columnar operations
```

**Layer Architecture**: Application → API (Writer/Reader) → Data Structures (Tablet/Schema) → Encoding → Compression → I/O

## TSFile Format Version Support

| Version | Read    | Write   | Notes                                    |
| ------- | ------- | ------- | ---------------------------------------- |
| v3      | ✅ Full | ✅ Full | Use `new TsFileWriter(path, 3)`          |
| v4      | ✅ Full | ✅ Full | Default: `new TsFileWriter(path)` or `new TsFileWriter(path, 4)` |

**V4 Format Features**:

- Table-based model with TAG and FIELD columns
- Aligned timeseries (separate time/value chunks)
- MetadataIndexNode tree structure
- Supports both tree model (Device/Measurement) and table model (Table/Column)

## Implementation Status

- **Data Types**: 13/13 (100% Java compatible)
- **Encodings**: 11/14 implemented (Plain, RLE, ZigZag, Gorilla, GorillaV1, Dictionary, TS_2DIFF, Diff, Bitmap, Regular, Freq)
- **Compression**: 5/6 (GZIP, LZ4, ZSTD, Snappy, Uncompressed; LZMA2 not available in .NET 10)
- **Tests**: 183 passing (182 pass, 1 skip)

Unimplemented encodings (CHIMP, SPRINTZ, RLBE) fallback to Plain encoding for compatibility.

## Key Files for C# Development

- `csharp/STATUS.md` - Comprehensive implementation status
- `csharp/DESIGN.md` - Architecture and design decisions
- `csharp/USER_MANUAL.md` - Complete API usage guide
- `docs/TSFILE_FORMAT_V4.md` - v4 format specification

## Dependencies (C#)

- K4os.Compression.LZ4 (1.3.8) - LZ4 compression
- ZstdSharp.Port (0.8.7) - ZSTD compression
- IronSnappy (1.3.1) - Snappy compression (pure C#, cross-platform)
- xUnit (2.9.2) - Testing framework

## Code Style

- C#: Follow .NET naming conventions, XML documentation for public APIs
- Java: Checkstyle configuration at `checkstyle.xml`

# 📁 文件修改规则

<FILE_MODIFICATION_RULE>

## 核心原则

Write 单次 < 150 行，Edit 单次 < 50 行，超过必须分块

## ⚠️ API 限制适配（重要）

由于 API 代理服务器对输出长度有限制，禁止一次性写入大文件。

Task 子代理使用同一 API，同样受限，所以分块写入是唯一解决方案。

## 分块写入流程

| 步骤 | 操作           | 限制         |
| ---- | -------------- | ------------ |
| 1    | Write 创建骨架 | < 100 行     |
| 2    | Edit 逐步添加  | 每次 < 50 行 |
| 3    | Read 验证      | 确认完整性   |

## 各类型文件的骨架示例

| 文件类型 | 骨架内容                       |
| -------- | ------------------------------ |
| 代码     | import + 类/函数签名（空实现） |
| Markdown | 标题 + 章节占位符              |
| JSON     | 基础结构 {} + 顶层 key         |
| YAML     | 顶层 key + 空值                |
| 配置     | 最小必需配置                   |

## 判断标准

| 操作     | 行数     | 方法                   |
| -------- | -------- | ---------------------- |
| 创建文件 | < 150 行 | Write 一次完成         |
| 创建文件 | > 150 行 | 分块：骨架 + 多次 Edit |
| 修改文件 | < 50 行  | Edit 一次完成          |
| 修改文件 | > 50 行  | 分多次 Edit            |

## 失败处理

```Plain
Write/Edit 失败 → 不要重试相同内容 → 改用更小的分块
```

### ❌ 禁止行为

- 禁止 Write 超过 150 行
- 禁止 Edit 超过 50 行
- 禁止重复尝试失败的工具
- 禁止使用 heredoc 写代码（特殊字符会失败）

</FILE_MODIFICATION_RULE>

## Active Technologies
- C# / .NET 10 + K4os.Compression.LZ4, ZstdSharp.Port, IronSnappy, xUni (001-unified-v4-api)
- 文件系统（TsFile 二进制格式） (001-unified-v4-api)
- C# / .NET 10 + K4os.Compression.LZ4 (1.3.8), ZstdSharp.Port (0.8.7), IronSnappy (1.3.1) (001-unified-v4-api)
- TsFile 二进制格式（V3/V4） (001-unified-v4-api)

## Recent Changes
- 001-unified-v4-api: Added C# / .NET 10 + K4os.Compression.LZ4, ZstdSharp.Port, IronSnappy, xUni
