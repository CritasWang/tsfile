# Apache TsFile - Rust Implementation

A Rust implementation of the Apache TsFile format, providing a columnar storage solution for time series data.

## Overview

This is a complete Rust implementation of the TsFile format, designed to be compatible with the C++ and Java implementations. TsFile is optimized for time series data storage with support for multiple data types, encodings, and compression methods.

## Features

- **Complete Data Type Support**: Boolean, Int32, Int64, Float, Double, String/Text, and more
- **Multiple Encoding Methods**: Plain, RLE, TS_2DIFF, Gorilla, Dictionary, Zigzag, and more
- **Compression Support**: Snappy, Gzip, LZ4 (via feature flags)
- **Efficient Batch Operations**: Tablet-based API for high-performance writes
- **Query Capabilities**: Time-range and filter-based queries
- **Zero-Copy Reading**: Efficient data access with minimal overhead
- **Type-Safe API**: Leverage Rust's type system for correctness

## Quick Start

### Installation

Add this to your `Cargo.toml`:

```toml
[dependencies]
tsfile = "2.2.1-SNAPSHOT"
```

### Writing Data

```rust
use tsfile::writer::TsFileWriter;
use tsfile::common::{ColumnSchema, TableSchema, TSDataType, ColumnCategory};
use tsfile::writer::Tablet;

// Create schema
let schema = TableSchema::new(
    "sensor_table",
    vec![
        ColumnSchema::tag("device_id", TSDataType::String),
        ColumnSchema::field("temperature", TSDataType::Float),
        ColumnSchema::field("humidity", TSDataType::Float),
    ],
);

// Create writer
let mut writer = TsFileWriter::new("data.tsfile", schema)?;

// Create and populate tablet
let mut tablet = Tablet::with_schema(
    "sensor_table",
    vec!["device_id", "temperature", "humidity"],
    vec![TSDataType::String, TSDataType::Float, TSDataType::Float],
    vec![ColumnCategory::Tag, ColumnCategory::Field, ColumnCategory::Field],
    100,
);

// Add data rows
for i in 0..10 {
    tablet.add_timestamp(i, i as i64 * 1000);
    tablet.add_value(i, "device_id", "sensor_001")?;
    tablet.add_value(i, "temperature", 20.0 + i as f32)?;
    tablet.add_value(i, "humidity", 50.0 + i as f32)?;
}

// Write and close
writer.write_tablet(&tablet)?;
writer.flush()?;
writer.close()?;
```

### Reading Data

```rust
use tsfile::reader::TsFileReader;

// Open file
let mut reader = TsFileReader::open("data.tsfile")?;

// Query data
let mut result_set = reader.query(
    "sensor_table",
    vec!["temperature", "humidity"],
    0,
    10000,
    None,
)?;

// Iterate results
while result_set.next()? {
    let timestamp = result_set.get_timestamp();
    let temp: f32 = result_set.get_value(1)?;
    let hum: f32 = result_set.get_value(2)?;
    println!("Time: {}, Temp: {:.2}°C, Humidity: {:.2}%", timestamp, temp, hum);
}

reader.close()?;
```

## Architecture

The implementation follows the same architecture as the C++ version:

```
src/
├── common/          # Core data types and schemas
│   ├── data_type.rs      # TSDataType enum
│   ├── encoding.rs       # TSEncoding enum
│   ├── compression.rs    # CompressionType enum
│   ├── schema.rs         # Schema definitions
│   └── time_range.rs     # Time range utilities
├── encoding/        # Encoding implementations
│   ├── plain.rs          # Plain encoding
│   ├── rle.rs            # Run-length encoding
│   ├── ts_2diff.rs       # Two-level differential
│   └── gorilla.rs        # Gorilla encoding
├── compress/        # Compression implementations
├── file/            # File I/O layer
├── writer/          # Write operations
│   ├── tablet.rs         # Tablet data structure
│   └── mod.rs            # TsFileWriter
└── reader/          # Read operations
    └── mod.rs            # TsFileReader
```

## Features

The crate supports optional compression features:

- `snappy` - Snappy compression (enabled by default)
- `gzip` - Gzip compression (enabled by default)
- `lz4` - LZ4 compression (enabled by default)
- `full` - All compression methods

To use specific features:

```toml
[dependencies]
tsfile = { version = "2.2.1-SNAPSHOT", default-features = false, features = ["snappy"] }
```

## Development Status

This implementation is currently under active development. The following components are complete:

- ✅ Core data types (TSDataType, TSEncoding, CompressionType)
- ✅ Schema management (ColumnSchema, TableSchema)
- ✅ Basic encoding (Plain)
- ✅ Compression (Snappy, Gzip, LZ4)
- ✅ File I/O layer
- ✅ Tablet structure
- 🚧 TsFileWriter (in progress)
- 🚧 TsFileReader (in progress)
- 🚧 Advanced encodings (RLE, TS_2DIFF, Gorilla, etc.)
- 🚧 Query filters
- 🚧 Performance optimizations

## Testing

Run tests with:

```bash
cargo test
```

Run benchmarks with:

```bash
cargo bench
```

## Performance

Performance benchmarks comparing with the C++ implementation will be available soon.

## License

This project is licensed under the Apache License 2.0 - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Compatibility

This implementation aims to be fully compatible with:
- Apache TsFile C++ implementation (reference)
- Apache TsFile Java implementation
- Apache TsFile Python implementation (Cython wrapper around C++)

Files written by this Rust implementation can be read by other implementations and vice versa.
