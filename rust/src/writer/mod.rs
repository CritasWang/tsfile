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

use crate::common::tsfile_constants::*;
use crate::common::{CompressionType, TSDataType, TSEncoding, TableSchema};
use crate::compress::compress;
use crate::encoding::PlainEncoder;
use crate::error::{Result, TsFileError};
use crate::file::WriteFile;
use crate::metadata::TsFileMetadata;
use crate::utils::{write_var_string, write_var_uint};
use byteorder::{LittleEndian, WriteBytesExt};
use std::path::Path;

/// TsFile writer for writing time series data
pub struct TsFileWriter {
    file: WriteFile,
    schema: TableSchema,
    closed: bool,
    metadata_offset: u64,
}

impl TsFileWriter {
    /// Create a new TsFile writer
    pub fn new<P: AsRef<Path>>(path: P, schema: TableSchema) -> Result<Self> {
        let mut file = WriteFile::create(path)?;

        // Write file header
        file.write(MAGIC_STRING_TSFILE)?;
        file.write(&[VERSION_NUM_BYTE])?;

        Ok(Self {
            file,
            schema,
            closed: false,
            metadata_offset: 0,
        })
    }

    /// Write a tablet of data
    ///
    /// This is a simplified implementation that writes data in plain format.
    /// A full implementation would support all encoding and compression methods.
    pub fn write_tablet(&mut self, tablet: &Tablet) -> Result<()> {
        if self.closed {
            return Err(TsFileError::write("Writer is closed"));
        }

        if tablet.row_count() == 0 {
            return Ok(());
        }

        // Write chunk group header with table name as device ID
        self.write_chunk_group_header(&tablet.table_name)?;

        // Write each column as a chunk
        for (i, col_name) in tablet.column_names.iter().enumerate() {
            let col_schema = self.schema.find_column(col_name).ok_or_else(|| {
                TsFileError::schema(format!("Column {} not found in schema", col_name))
            })?;

            self.write_chunk(
                col_name,
                col_schema.data_type,
                col_schema.encoding,
                col_schema.compression,
                tablet,
                i,
            )?;
        }

        Ok(())
    }

    /// Write chunk group header (device identifier)
    fn write_chunk_group_header(&mut self, device_id: &str) -> Result<()> {
        self.file.write(&[CHUNK_GROUP_HEADER_MARKER])?;
        let mut buf = Vec::new();
        write_var_string(&mut buf, device_id)?;
        self.file.write(&buf)?;
        Ok(())
    }

    /// Write a single chunk (simplified version)
    fn write_chunk(
        &mut self,
        measurement_name: &str,
        data_type: TSDataType,
        encoding: TSEncoding,
        compression: CompressionType,
        tablet: &Tablet,
        col_index: usize,
    ) -> Result<()> {
        // Encode the data
        let encoded_data = self.encode_column_data(tablet, col_index, data_type, encoding)?;

        // Compress the data
        let compressed_data = compress(&encoded_data, compression)?;

        // Write chunk header
        self.file.write(&[ONLY_ONE_PAGE_CHUNK_HEADER_MARKER])?; // Single page chunk

        let mut header_buf = Vec::new();
        write_var_string(&mut header_buf, measurement_name)?;
        write_var_uint(&mut header_buf, compressed_data.len() as u64)?; // data size
        header_buf.write_u8(data_type.to_byte())?;
        header_buf.write_u8(compression.to_byte())?;
        header_buf.write_u8(encoding.to_byte())?;

        self.file.write(&header_buf)?;

        // Write compressed data
        self.file.write(&compressed_data)?;

        Ok(())
    }

    /// Encode column data (simplified - only supports plain encoding for now)
    fn encode_column_data(
        &self,
        tablet: &Tablet,
        col_index: usize,
        _data_type: TSDataType,
        _encoding: TSEncoding,
    ) -> Result<Vec<u8>> {
        let mut buf = Vec::new();

        // Write timestamps first
        write_var_uint(&mut buf, tablet.timestamps.len() as u64)?;
        for &ts in &tablet.timestamps {
            PlainEncoder::encode_i64(&mut buf, ts)?;
        }

        // Write values
        if let Some(col_values) = tablet.values.get(&tablet.column_names[col_index]) {
            match col_values {
                tablet::ColumnValue::Boolean(vals) => {
                    for val in vals {
                        PlainEncoder::encode_bool(&mut buf, val.unwrap_or(false))?;
                    }
                }
                tablet::ColumnValue::Int32(vals) => {
                    for val in vals {
                        PlainEncoder::encode_i32(&mut buf, val.unwrap_or(0))?;
                    }
                }
                tablet::ColumnValue::Int64(vals) => {
                    for val in vals {
                        PlainEncoder::encode_i64(&mut buf, val.unwrap_or(0))?;
                    }
                }
                tablet::ColumnValue::Float(vals) => {
                    for val in vals {
                        PlainEncoder::encode_f32(&mut buf, val.unwrap_or(0.0))?;
                    }
                }
                tablet::ColumnValue::Double(vals) => {
                    for val in vals {
                        PlainEncoder::encode_f64(&mut buf, val.unwrap_or(0.0))?;
                    }
                }
                tablet::ColumnValue::String(vals) => {
                    for val in vals {
                        PlainEncoder::encode_string(&mut buf, val.as_deref().unwrap_or(""))?;
                    }
                }
            }
        }

        Ok(buf)
    }

    /// Flush buffered data to disk
    pub fn flush(&mut self) -> Result<()> {
        if self.closed {
            return Err(TsFileError::write("Writer is closed"));
        }
        self.file.flush()?;
        Ok(())
    }

    /// Close the writer
    pub fn close(&mut self) -> Result<()> {
        if !self.closed {
            // Write minimal index section
            self.write_minimal_index()?;

            // Write file footer
            self.write_file_footer()?;

            self.file.flush()?;
            self.closed = true;
        }
        Ok(())
    }

    /// Write metadata section (compatible with Java/C++ format)
    fn write_minimal_index(&mut self) -> Result<()> {
        self.metadata_offset = self.file.position();

        // Write separator marker
        self.file.write(&[SEPARATOR_MARKER])?;

        // Create TsFile metadata structure
        let metadata = TsFileMetadata::with_table(
            self.schema.table_name.clone(),
            self.schema.clone(),
            self.metadata_offset as i64,
        );

        // Serialize metadata to a buffer first to ensure proper format
        let mut metadata_buffer = Vec::new();
        metadata.serialize_to(&mut metadata_buffer)?;

        // Write the metadata buffer to file
        self.file.write(&metadata_buffer)?;

        Ok(())
    }

    /// Write file footer
    fn write_file_footer(&mut self) -> Result<()> {
        // Calculate metadata size
        let current_pos = self.file.position();
        let metadata_size = (current_pos - self.metadata_offset) as i32;

        // Write magic string
        self.file.write(MAGIC_STRING_TSFILE)?;

        // Write metadata size
        let mut buf = Vec::new();
        buf.write_i32::<LittleEndian>(metadata_size)?;
        self.file.write(&buf)?;

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
    use crate::common::ColumnSchema;
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

    #[test]
    fn test_write_and_close() {
        let temp_file = NamedTempFile::new().unwrap();
        let schema = TableSchema::new(
            "test_table",
            vec![ColumnSchema::field("temperature", TSDataType::Float)],
        );

        let mut writer = TsFileWriter::new(temp_file.path(), schema).unwrap();
        writer.close().unwrap();

        // Verify file has magic string at start and end
        use std::fs;
        let contents = fs::read(temp_file.path()).unwrap();
        assert!(contents.len() >= 17); // At least header + footer
        assert_eq!(&contents[0..6], b"TsFile");
        assert_eq!(
            &contents[contents.len() - 10..contents.len() - 4],
            b"TsFile"
        );
    }
}
