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

//! Run-Length Encoding (RLE) implementation
//!
//! RLE is a simple encoding method that compresses sequences of repeated values.
//! It's particularly effective for data with many consecutive identical values.

use crate::error::Result;
use byteorder::{LittleEndian, ReadBytesExt, WriteBytesExt};
use std::io::{Read, Write};

/// RLE encoder for boolean values
pub struct RleEncoder;

impl RleEncoder {
    /// Encode a boolean array using RLE
    ///
    /// Format: [count: i32, value: u8, count: i32, value: u8, ...]
    /// where count is the number of consecutive identical values
    pub fn encode_bool_array<W: Write>(writer: &mut W, values: &[bool]) -> Result<()> {
        if values.is_empty() {
            return Ok(());
        }

        let mut current_value = values[0];
        let mut count = 1;

        for &value in &values[1..] {
            if value == current_value && count < i32::MAX as usize {
                count += 1;
            } else {
                // Write the run
                writer.write_i32::<LittleEndian>(count as i32)?;
                writer.write_u8(current_value as u8)?;

                // Start new run
                current_value = value;
                count = 1;
            }
        }

        // Write the last run
        writer.write_i32::<LittleEndian>(count as i32)?;
        writer.write_u8(current_value as u8)?;

        Ok(())
    }

    /// Decode an RLE-encoded boolean array
    pub fn decode_bool_array<R: Read>(reader: &mut R, expected_count: usize) -> Result<Vec<bool>> {
        let mut result = Vec::with_capacity(expected_count);
        let mut total_decoded = 0;

        while total_decoded < expected_count {
            let count = reader.read_i32::<LittleEndian>()? as usize;
            let value = reader.read_u8()? != 0;

            if total_decoded + count > expected_count {
                return Err(crate::error::TsFileError::Decoding(
                    "RLE decode: count exceeds expected size".to_string(),
                ));
            }

            for _ in 0..count {
                result.push(value);
            }
            total_decoded += count;
        }

        Ok(result)
    }

    /// Encode an i32 array using RLE
    pub fn encode_i32_array<W: Write>(writer: &mut W, values: &[i32]) -> Result<()> {
        if values.is_empty() {
            return Ok(());
        }

        let mut current_value = values[0];
        let mut count = 1;

        for &value in &values[1..] {
            if value == current_value && count < i32::MAX as usize {
                count += 1;
            } else {
                // Write the run
                writer.write_i32::<LittleEndian>(count as i32)?;
                writer.write_i32::<LittleEndian>(current_value)?;

                // Start new run
                current_value = value;
                count = 1;
            }
        }

        // Write the last run
        writer.write_i32::<LittleEndian>(count as i32)?;
        writer.write_i32::<LittleEndian>(current_value)?;

        Ok(())
    }

    /// Decode an RLE-encoded i32 array
    pub fn decode_i32_array<R: Read>(reader: &mut R, expected_count: usize) -> Result<Vec<i32>> {
        let mut result = Vec::with_capacity(expected_count);
        let mut total_decoded = 0;

        while total_decoded < expected_count {
            let count = reader.read_i32::<LittleEndian>()? as usize;
            let value = reader.read_i32::<LittleEndian>()?;

            if total_decoded + count > expected_count {
                return Err(crate::error::TsFileError::Decoding(
                    "RLE decode: count exceeds expected size".to_string(),
                ));
            }

            for _ in 0..count {
                result.push(value);
            }
            total_decoded += count;
        }

        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;

    #[test]
    fn test_rle_bool_simple() {
        let values = vec![true, true, true, false, false, true];
        let mut buf = Vec::new();
        RleEncoder::encode_bool_array(&mut buf, &values).unwrap();

        let mut cursor = Cursor::new(buf);
        let decoded = RleEncoder::decode_bool_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_rle_bool_all_same() {
        let values = vec![true; 100];
        let mut buf = Vec::new();
        RleEncoder::encode_bool_array(&mut buf, &values).unwrap();

        // Should compress to a single run
        assert!(buf.len() < 100);

        let mut cursor = Cursor::new(buf);
        let decoded = RleEncoder::decode_bool_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_rle_bool_alternating() {
        let values = vec![true, false, true, false, true, false];
        let mut buf = Vec::new();
        RleEncoder::encode_bool_array(&mut buf, &values).unwrap();

        // Alternating pattern won't compress well, but should encode/decode correctly
        let mut cursor = Cursor::new(buf);
        let decoded = RleEncoder::decode_bool_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_rle_i32_simple() {
        let values = vec![1, 1, 1, 2, 2, 3, 3, 3, 3];
        let mut buf = Vec::new();
        RleEncoder::encode_i32_array(&mut buf, &values).unwrap();

        let mut cursor = Cursor::new(buf);
        let decoded = RleEncoder::decode_i32_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_rle_i32_all_same() {
        let values = vec![42; 1000];
        let mut buf = Vec::new();
        RleEncoder::encode_i32_array(&mut buf, &values).unwrap();

        // Should compress to a single run (4 bytes count + 4 bytes value = 8 bytes)
        assert_eq!(buf.len(), 8);

        let mut cursor = Cursor::new(buf);
        let decoded = RleEncoder::decode_i32_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_rle_i32_no_compression() {
        let values: Vec<i32> = (0..100).collect();
        let mut buf = Vec::new();
        RleEncoder::encode_i32_array(&mut buf, &values).unwrap();

        // Each value is unique, so no compression benefit
        // Each run: 4 bytes count + 4 bytes value = 8 bytes per value
        assert_eq!(buf.len(), 800);

        let mut cursor = Cursor::new(buf);
        let decoded = RleEncoder::decode_i32_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_rle_empty() {
        let values: Vec<bool> = vec![];
        let mut buf = Vec::new();
        RleEncoder::encode_bool_array(&mut buf, &values).unwrap();
        assert_eq!(buf.len(), 0);

        let mut cursor = Cursor::new(buf);
        let decoded = RleEncoder::decode_bool_array(&mut cursor, 0).unwrap();
        assert_eq!(decoded, values);
    }
}
