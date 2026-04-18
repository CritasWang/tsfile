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

//! TsFile writer implementation

mod tablet;

pub use tablet::Tablet;

use crate::common::{TableSchema, Timestamp};
use crate::error::Result;
use crate::file::WriteFile;
use std::path::Path;

/// TsFile writer for writing time series data
pub struct TsFileWriter {
    file: WriteFile,
    schema: TableSchema,
    closed: bool,
}

impl TsFileWriter {
    /// Create a new TsFile writer
    pub fn new<P: AsRef<Path>>(path: P, schema: TableSchema) -> Result<Self> {
        let file = WriteFile::create(path)?;
        Ok(Self {
            file,
            schema,
            closed: false,
        })
    }

    /// Write a tablet of data
    pub fn write_tablet(&mut self, _tablet: &Tablet) -> Result<()> {
        if self.closed {
            return Err(crate::error::TsFileError::write("Writer is closed"));
        }
        // TODO: Implement tablet writing
        Ok(())
    }

    /// Flush buffered data to disk
    pub fn flush(&mut self) -> Result<()> {
        if self.closed {
            return Err(crate::error::TsFileError::write("Writer is closed"));
        }
        self.file.flush()?;
        Ok(())
    }

    /// Close the writer
    pub fn close(&mut self) -> Result<()> {
        if !self.closed {
            self.flush()?;
            self.closed = true;
        }
        Ok(())
    }
}

impl Drop for TsFileWriter {
    fn drop(&mut self) {
        let _ = self.close();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::common::{ColumnSchema, TSDataType, ColumnCategory, CompressionType, TSEncoding};
    use tempfile::NamedTempFile;

    #[test]
    fn test_writer_creation() {
        let temp_file = NamedTempFile::new().unwrap();
        let schema = TableSchema::new(
            "test_table",
            vec![
                ColumnSchema::tag("id1", TSDataType::String),
                ColumnSchema::field("s1", TSDataType::Int64),
            ],
        );
        let writer = TsFileWriter::new(temp_file.path(), schema);
        assert!(writer.is_ok());
    }
}
