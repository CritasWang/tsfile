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

//! Two-level differential encoding (TS_2DIFF)
//!
//! TS_2DIFF encoding computes the second-order difference (delta of deltas)
//! which is effective for monotonically increasing sequences like timestamps.
//! It stores:
//! 1. The first value
//! 2. The first delta
//! 3. The delta of deltas for subsequent values

use crate::error::Result;
use byteorder::{LittleEndian, ReadBytesExt, WriteBytesExt};
use std::io::{Read, Write};

/// TS_2DIFF encoder for integer values
pub struct Ts2DiffEncoder;

impl Ts2DiffEncoder {
    /// Encode i32 values using TS_2DIFF
    pub fn encode_i32_array<W: Write>(writer: &mut W, values: &[i32]) -> Result<()> {
        if values.is_empty() {
            return Ok(());
        }

        // Write first value
        writer.write_i32::<LittleEndian>(values[0])?;

        if values.len() == 1 {
            return Ok(());
        }

        // Write first delta
        let first_delta = values[1] - values[0];
        writer.write_i32::<LittleEndian>(first_delta)?;

        if values.len() == 2 {
            return Ok(());
        }

        // Write second-order differences
        let mut prev_delta = first_delta;
        for i in 2..values.len() {
            let delta = values[i] - values[i - 1];
            let delta_of_delta = delta - prev_delta;
            writer.write_i32::<LittleEndian>(delta_of_delta)?;
            prev_delta = delta;
        }

        Ok(())
    }

    /// Decode TS_2DIFF encoded i32 values
    pub fn decode_i32_array<R: Read>(reader: &mut R, count: usize) -> Result<Vec<i32>> {
        if count == 0 {
            return Ok(Vec::new());
        }

        let mut result = Vec::with_capacity(count);

        // Read first value
        let first_value = reader.read_i32::<LittleEndian>()?;
        result.push(first_value);

        if count == 1 {
            return Ok(result);
        }

        // Read first delta
        let first_delta = reader.read_i32::<LittleEndian>()?;
        result.push(first_value + first_delta);

        if count == 2 {
            return Ok(result);
        }

        // Read and reconstruct from delta-of-deltas
        let mut prev_delta = first_delta;
        for _ in 2..count {
            let delta_of_delta = reader.read_i32::<LittleEndian>()?;
            let delta = prev_delta + delta_of_delta;
            let value = result[result.len() - 1] + delta;
            result.push(value);
            prev_delta = delta;
        }

        Ok(result)
    }

    /// Encode i64 values using TS_2DIFF
    pub fn encode_i64_array<W: Write>(writer: &mut W, values: &[i64]) -> Result<()> {
        if values.is_empty() {
            return Ok(());
        }

        // Write first value
        writer.write_i64::<LittleEndian>(values[0])?;

        if values.len() == 1 {
            return Ok(());
        }

        // Write first delta
        let first_delta = values[1] - values[0];
        writer.write_i64::<LittleEndian>(first_delta)?;

        if values.len() == 2 {
            return Ok(());
        }

        // Write second-order differences
        let mut prev_delta = first_delta;
        for i in 2..values.len() {
            let delta = values[i] - values[i - 1];
            let delta_of_delta = delta - prev_delta;
            writer.write_i64::<LittleEndian>(delta_of_delta)?;
            prev_delta = delta;
        }

        Ok(())
    }

    /// Decode TS_2DIFF encoded i64 values
    pub fn decode_i64_array<R: Read>(reader: &mut R, count: usize) -> Result<Vec<i64>> {
        if count == 0 {
            return Ok(Vec::new());
        }

        let mut result = Vec::with_capacity(count);

        // Read first value
        let first_value = reader.read_i64::<LittleEndian>()?;
        result.push(first_value);

        if count == 1 {
            return Ok(result);
        }

        // Read first delta
        let first_delta = reader.read_i64::<LittleEndian>()?;
        result.push(first_value + first_delta);

        if count == 2 {
            return Ok(result);
        }

        // Read and reconstruct from delta-of-deltas
        let mut prev_delta = first_delta;
        for _ in 2..count {
            let delta_of_delta = reader.read_i64::<LittleEndian>()?;
            let delta = prev_delta + delta_of_delta;
            let value = result[result.len() - 1] + delta;
            result.push(value);
            prev_delta = delta;
        }

        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;

    #[test]
    fn test_ts2diff_i32_constant_delta() {
        // Timestamps with constant increment
        let values: Vec<i32> = (0..100).map(|i| i * 1000).collect();
        let mut buf = Vec::new();
        Ts2DiffEncoder::encode_i32_array(&mut buf, &values).unwrap();

        // First value (4 bytes) + first delta (4 bytes) + 98 zeros (98*4 = 392 bytes)
        // Total: 400 bytes vs 400 bytes uncompressed
        // But with compression, the zeros compress very well
        println!("Encoded size for constant delta: {} bytes", buf.len());

        let mut cursor = Cursor::new(buf);
        let decoded = Ts2DiffEncoder::decode_i32_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_ts2diff_i32_linear() {
        // Linear sequence: 0, 1, 2, 3, ...
        let values: Vec<i32> = (0..50).collect();
        let mut buf = Vec::new();
        Ts2DiffEncoder::encode_i32_array(&mut buf, &values).unwrap();

        let mut cursor = Cursor::new(buf);
        let decoded = Ts2DiffEncoder::decode_i32_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_ts2diff_i64_timestamps() {
        // Simulated timestamps with mostly constant interval
        let mut values = Vec::new();
        let start = 1609459200000i64; // 2021-01-01 00:00:00
        let interval = 1000i64; // 1 second

        for i in 0..100 {
            values.push(start + i * interval);
        }

        let mut buf = Vec::new();
        Ts2DiffEncoder::encode_i64_array(&mut buf, &values).unwrap();

        println!("Encoded size for timestamps: {} bytes", buf.len());

        let mut cursor = Cursor::new(buf);
        let decoded = Ts2DiffEncoder::decode_i64_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_ts2diff_i32_irregular() {
        let values = vec![5, 10, 12, 19, 22, 30];
        let mut buf = Vec::new();
        Ts2DiffEncoder::encode_i32_array(&mut buf, &values).unwrap();

        let mut cursor = Cursor::new(buf);
        let decoded = Ts2DiffEncoder::decode_i32_array(&mut cursor, values.len()).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_ts2diff_empty() {
        let values: Vec<i32> = vec![];
        let mut buf = Vec::new();
        Ts2DiffEncoder::encode_i32_array(&mut buf, &values).unwrap();
        assert_eq!(buf.len(), 0);

        let mut cursor = Cursor::new(buf);
        let decoded = Ts2DiffEncoder::decode_i32_array(&mut cursor, 0).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_ts2diff_single_value() {
        let values = vec![42i32];
        let mut buf = Vec::new();
        Ts2DiffEncoder::encode_i32_array(&mut buf, &values).unwrap();
        assert_eq!(buf.len(), 4); // Just the value

        let mut cursor = Cursor::new(buf);
        let decoded = Ts2DiffEncoder::decode_i32_array(&mut cursor, 1).unwrap();
        assert_eq!(decoded, values);
    }

    #[test]
    fn test_ts2diff_two_values() {
        let values = vec![10i32, 20i32];
        let mut buf = Vec::new();
        Ts2DiffEncoder::encode_i32_array(&mut buf, &values).unwrap();
        assert_eq!(buf.len(), 8); // Value + delta

        let mut cursor = Cursor::new(buf);
        let decoded = Ts2DiffEncoder::decode_i32_array(&mut cursor, 2).unwrap();
        assert_eq!(decoded, values);
    }
}
