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

//! TsFile reader implementation

use crate::common::tsfile_constants::*;
use crate::common::{CompressionType, TSDataType, TSEncoding, TimeRange, Timestamp};
use crate::compress::decompress;
use crate::encoding::PlainEncoder;
use crate::error::{Result, TsFileError};
use crate::file::ReadFile;
use crate::utils::{read_var_string, read_var_uint};
use std::io::SeekFrom;
use std::path::Path;

/// TsFile reader for reading time series data
pub struct TsFileReader {
    file: ReadFile,
    closed: bool,
}

impl TsFileReader {
    /// Open a TsFile for reading
    pub fn open<P: AsRef<Path>>(path: P) -> Result<Self> {
        let mut file = ReadFile::open(path)?;

        // Verify file header
        let mut magic = vec![0u8; 6];
        file.read_exact(&mut magic)?;
        if magic != MAGIC_STRING_TSFILE {
            return Err(TsFileError::format("Invalid TsFile magic header"));
        }

        let mut version = [0u8; 1];
        file.read_exact(&mut version)?;
        if version[0] != VERSION_NUM_BYTE {
            return Err(TsFileError::format(format!(
                "Unsupported TsFile version: 0x{:02x}",
                version[0]
            )));
        }

        Ok(Self {
            file,
            closed: false,
        })
    }

    /// Read all data from the file (simplified reader)
    ///
    /// Returns a ResultSet containing all chunks in the file.
    /// This is a simplified implementation that reads all data sequentially.
    pub fn read_all(&mut self) -> Result<ResultSet> {
        self.read_with_filter(None)
    }

    /// Read data with optional time range filter
    ///
    /// Returns a ResultSet containing chunks within the specified time range.
    /// If filter is None, returns all data.
    pub fn read_with_filter(&mut self, time_range: Option<TimeRange>) -> Result<ResultSet> {
        if self.closed {
            return Err(TsFileError::read("Reader is closed"));
        }

        let mut all_data = Vec::new();

        // Reset to start of data (after header)
        self.file.seek(SeekFrom::Start(7))?;

        // Read until we hit the separator marker
        loop {
            let mut marker = [0u8; 1];
            if self.file.read(&mut marker)? == 0 {
                break; // EOF
            }

            match marker[0] {
                CHUNK_GROUP_HEADER_MARKER => {
                    // Read device ID
                    let _device_id = self.read_device_id()?;
                    // Continue reading chunks in this group
                }
                ONLY_ONE_PAGE_CHUNK_HEADER_MARKER => {
                    // Read chunk
                    let chunk_data = self.read_chunk()?;

                    // Apply time range filter
                    if let Some(ref range) = time_range {
                        all_data.extend(
                            chunk_data
                                .into_iter()
                                .filter(|row| range.contains(row.timestamp)),
                        );
                    } else {
                        all_data.extend(chunk_data);
                    }
                }
                SEPARATOR_MARKER => {
                    // Hit the metadata section, stop reading data
                    break;
                }
                _ => {
                    // Unknown marker, might be part of data or other chunk type
                    // For now, skip
                }
            }
        }

        Ok(ResultSet::new(all_data))
    }

    /// Read device ID from chunk group header
    fn read_device_id(&mut self) -> Result<String> {
        read_var_string(&mut self.file)
    }

    /// Read a single chunk
    fn read_chunk(&mut self) -> Result<Vec<Row>> {
        // Read chunk header
        let measurement_name = read_var_string(&mut self.file)?;
        let data_size = read_var_uint(&mut self.file)? as usize;

        let mut data_type_byte = [0u8; 1];
        self.file.read_exact(&mut data_type_byte)?;
        let data_type = TSDataType::from_byte(data_type_byte[0]);

        let mut compression_byte = [0u8; 1];
        self.file.read_exact(&mut compression_byte)?;
        let compression = CompressionType::from_byte(compression_byte[0]);

        let mut encoding_byte = [0u8; 1];
        self.file.read_exact(&mut encoding_byte)?;
        let _encoding = TSEncoding::from_byte(encoding_byte[0]);

        // Read compressed data
        let mut compressed_data = vec![0u8; data_size];
        self.file.read_exact(&mut compressed_data)?;

        // Decompress
        let encoded_data = decompress(&compressed_data, compression, None)?;

        // Decode
        let rows = self.decode_chunk_data(&encoded_data, data_type, &measurement_name)?;

        Ok(rows)
    }

    /// Decode chunk data
    fn decode_chunk_data(
        &self,
        data: &[u8],
        data_type: TSDataType,
        column_name: &str,
    ) -> Result<Vec<Row>> {
        let mut cursor = std::io::Cursor::new(data);

        // Read timestamps
        let num_timestamps = read_var_uint(&mut cursor)? as usize;
        let mut timestamps = Vec::with_capacity(num_timestamps);
        for _ in 0..num_timestamps {
            timestamps.push(PlainEncoder::decode_i64(&mut cursor)?);
        }

        // Read values
        let mut rows = Vec::with_capacity(num_timestamps);
        for &timestamp in timestamps.iter().take(num_timestamps) {
            let value_str = match data_type {
                TSDataType::Boolean => {
                    let v = PlainEncoder::decode_bool(&mut cursor)?;
                    v.to_string()
                }
                TSDataType::Int32 | TSDataType::Date => {
                    let v = PlainEncoder::decode_i32(&mut cursor)?;
                    v.to_string()
                }
                TSDataType::Int64 | TSDataType::Timestamp => {
                    let v = PlainEncoder::decode_i64(&mut cursor)?;
                    v.to_string()
                }
                TSDataType::Float => {
                    let v = PlainEncoder::decode_f32(&mut cursor)?;
                    v.to_string()
                }
                TSDataType::Double => {
                    let v = PlainEncoder::decode_f64(&mut cursor)?;
                    v.to_string()
                }
                TSDataType::String | TSDataType::Text | TSDataType::Blob => {
                    PlainEncoder::decode_string(&mut cursor)?
                }
                _ => "".to_string(),
            };

            rows.push(Row {
                timestamp,
                values: vec![Some(value_str)],
                column_name: column_name.to_string(),
            });
        }

        Ok(rows)
    }

    /// Close the reader
    pub fn close(&mut self) -> Result<()> {
        if !self.closed {
            self.closed = true;
        }
        Ok(())
    }
}

impl Drop for TsFileReader {
    fn drop(&mut self) {
        let _ = self.close();
    }
}

/// Result set from a query
pub struct ResultSet {
    rows: Vec<Row>,
    current: usize,
}

impl ResultSet {
    /// Create a new result set
    pub fn new(rows: Vec<Row>) -> Self {
        Self { rows, current: 0 }
    }

    /// Move to the next row
    #[allow(clippy::should_implement_trait)]
    pub fn next(&mut self) -> Result<bool> {
        if self.current < self.rows.len() {
            self.current += 1;
            Ok(true)
        } else {
            Ok(false)
        }
    }

    /// Get the timestamp of the current row
    pub fn get_timestamp(&self) -> Timestamp {
        if self.current > 0 && self.current <= self.rows.len() {
            self.rows[self.current - 1].timestamp
        } else {
            0
        }
    }

    /// Get a string value from the current row
    pub fn get_string_value(&self, column_index: usize) -> Result<String> {
        if self.current == 0 || self.current > self.rows.len() {
            return Err(TsFileError::read("No current row"));
        }

        let row = &self.rows[self.current - 1];
        if column_index >= row.values.len() {
            return Err(TsFileError::InvalidColumnIndex(column_index));
        }

        Ok(row.values[column_index]
            .as_ref()
            .unwrap_or(&"".to_string())
            .clone())
    }

    /// Get the column name of the current row
    pub fn get_column_name(&self) -> Result<String> {
        if self.current == 0 || self.current > self.rows.len() {
            return Err(TsFileError::read("No current row"));
        }
        Ok(self.rows[self.current - 1].column_name.clone())
    }

    /// Get the number of rows
    pub fn row_count(&self) -> usize {
        self.rows.len()
    }

    /// Close the result set
    pub fn close(&mut self) -> Result<()> {
        Ok(())
    }
}

/// A row in a result set
#[derive(Debug, Clone)]
pub struct Row {
    pub timestamp: Timestamp,
    pub values: Vec<Option<String>>,
    pub column_name: String,
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::common::{ColumnCategory, ColumnSchema, TableSchema};
    use crate::writer::{Tablet, TsFileWriter};
    use tempfile::NamedTempFile;

    #[test]
    fn test_reader_open() {
        // Create a temporary file
        let temp_file = NamedTempFile::new().unwrap();
        {
            let mut writer = crate::file::WriteFile::create(temp_file.path()).unwrap();
            writer.write(b"test").unwrap();
            writer.close().unwrap();
        }

        // Try to open it - should fail because it's not a valid TsFile
        let reader = TsFileReader::open(temp_file.path());
        assert!(reader.is_err());
    }

    #[test]
    fn test_write_and_read() {
        let temp_file = NamedTempFile::new().unwrap();
        let path = temp_file.path();

        // Write data
        {
            let schema = TableSchema::new(
                "test_table",
                vec![
                    ColumnSchema::field("temperature", TSDataType::Float),
                    ColumnSchema::field("humidity", TSDataType::Float),
                ],
            );

            let mut writer = TsFileWriter::new(path, schema).unwrap();

            let mut tablet = Tablet::with_schema(
                "test_table",
                vec!["temperature", "humidity"],
                vec![TSDataType::Float, TSDataType::Float],
                vec![ColumnCategory::Field, ColumnCategory::Field],
                10,
            );

            for i in 0..5 {
                tablet.add_timestamp(i, (i * 1000) as i64);
                tablet.add_value(i, "temperature", 20.0 + i as f32).unwrap();
                tablet.add_value(i, "humidity", 50.0 + i as f32).unwrap();
            }

            writer.write_tablet(&tablet).unwrap();
            writer.close().unwrap();
        }

        // Read data
        {
            let mut reader = TsFileReader::open(path).unwrap();
            let mut result_set = reader.read_all().unwrap();

            assert!(result_set.row_count() > 0);

            let mut count = 0;
            while result_set.next().unwrap() {
                let _timestamp = result_set.get_timestamp();
                let _value = result_set.get_string_value(0).unwrap();
                count += 1;
            }

            assert_eq!(count, result_set.row_count());

            reader.close().unwrap();
        }
    }

    #[test]
    fn test_read_with_time_range_filter() {
        let temp_file = NamedTempFile::new().unwrap();
        let path = temp_file.path();

        // Write data with timestamps 0, 1000, 2000, 3000, 4000
        {
            let schema = TableSchema::new(
                "test_table",
                vec![ColumnSchema::field("temperature", TSDataType::Float)],
            );

            let mut writer = TsFileWriter::new(path, schema).unwrap();

            let mut tablet = Tablet::with_schema(
                "test_table",
                vec!["temperature"],
                vec![TSDataType::Float],
                vec![ColumnCategory::Field],
                10,
            );

            for i in 0..5 {
                tablet.add_timestamp(i, (i * 1000) as i64);
                tablet.add_value(i, "temperature", 20.0 + i as f32).unwrap();
            }

            writer.write_tablet(&tablet).unwrap();
            writer.close().unwrap();
        }

        // Read data with time range filter [1000, 3000] (inclusive)
        {
            let mut reader = TsFileReader::open(path).unwrap();
            let time_range = TimeRange::new(1000, 3000);
            let mut result_set = reader.read_with_filter(Some(time_range)).unwrap();

            // Should get timestamps 1000, 2000, and 3000 (3 rows, inclusive range)
            assert_eq!(result_set.row_count(), 3);

            let mut timestamps = Vec::new();
            while result_set.next().unwrap() {
                timestamps.push(result_set.get_timestamp());
            }

            assert_eq!(timestamps, vec![1000, 2000, 3000]);

            reader.close().unwrap();
        }
    }
}
