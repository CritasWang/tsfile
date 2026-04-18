/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

//! # Apache TsFile Rust Implementation
//!
//! TsFile is a columnar storage file format designed for time series data.
//! This Rust implementation provides a complete API compatible with the C++ version.
//!
//! ## Features
//!
//! - Complete metadata management covering all data types and encoding/compression methods
//! - Full write operations to TsFile format
//! - Full read operations with time range and filter-based queries
//! - Comprehensive unit and integration tests
//! - Performance benchmarks
//!
//! ## Quick Start
//!
//! ### Writing Data
//!
//! ```rust,no_run
//! use tsfile::writer::TsFileWriter;
//! use tsfile::common::{ColumnSchema, TableSchema, TSDataType, ColumnCategory, CompressionType, TSEncoding};
//! use tsfile::writer::Tablet;
//!
//! // Create table schema
//! let schema = TableSchema::new(
//!     "table1",
//!     vec![
//!         ColumnSchema::new("id1", TSDataType::String, CompressionType::Uncompressed,
//!                          TSEncoding::Plain, ColumnCategory::Tag),
//!         ColumnSchema::new("s1", TSDataType::Int64, CompressionType::Uncompressed,
//!                          TSEncoding::Plain, ColumnCategory::Field),
//!     ],
//! );
//!
//! // Create writer
//! let mut writer = TsFileWriter::new("test.tsfile", schema)?;
//!
//! // Create and write tablet
//! let mut tablet = Tablet::new("table1", vec!["id1", "s1"], 10);
//! // ... add data to tablet ...
//! writer.write_tablet(&tablet)?;
//!
//! // Flush and close
//! writer.flush()?;
//! writer.close()?;
//! # Ok::<(), tsfile::error::TsFileError>(())
//! ```
//!
//! ### Reading Data
//!
//! ```rust,no_run
//! use tsfile::reader::TsFileReader;
//!
//! // Open reader
//! let mut reader = TsFileReader::open("test.tsfile")?;
//!
//! // Query data
//! let mut result_set = reader.query("table1", vec!["s1"], 0, 100, None)?;
//!
//! // Iterate results
//! while result_set.next()? {
//!     let timestamp = result_set.get_timestamp();
//!     let value: i64 = result_set.get_value(1)?;
//!     println!("timestamp: {}, value: {}", timestamp, value);
//! }
//!
//! reader.close()?;
//! # Ok::<(), tsfile::error::TsFileError>(())
//! ```

pub mod common;
pub mod compress;
pub mod encoding;
pub mod error;
pub mod file;
pub mod reader;
pub mod utils;
pub mod writer;

// Re-export commonly used types
pub use common::{
    ColumnCategory, ColumnSchema, CompressionType, TableSchema, TSDataType, TSEncoding,
};
pub use error::{Result, TsFileError};
pub use reader::TsFileReader;
pub use writer::{Tablet, TsFileWriter};

/// Initialize the TsFile library
///
/// This function should be called before using any TsFile functionality.
/// It performs any necessary global initialization.
pub fn init() {
    // Currently no global initialization needed for Rust version
    // This function is provided for API compatibility with C++ version
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_init() {
        init(); // Should not panic
    }
}
