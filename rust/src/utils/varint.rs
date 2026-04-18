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

//! Variable-length integer encoding utilities

use crate::error::Result;
use std::io::{Read, Write};

/// Write a variable-length unsigned integer (varint)
///
/// Uses 7 bits per byte for data, with the high bit as continuation flag.
/// This matches the C++ ReadWriteIOUtils varint implementation.
pub fn write_var_uint<W: Write>(writer: &mut W, mut value: u64) -> Result<usize> {
    let mut bytes_written = 0;

    loop {
        let mut byte = (value & 0x7F) as u8;
        value >>= 7;

        if value != 0 {
            byte |= 0x80; // Set continuation bit
        }

        writer.write_all(&[byte])?;
        bytes_written += 1;

        if value == 0 {
            break;
        }
    }

    Ok(bytes_written)
}

/// Read a variable-length unsigned integer (varint)
pub fn read_var_uint<R: Read>(reader: &mut R) -> Result<u64> {
    let mut result = 0u64;
    let mut shift = 0;

    loop {
        let mut byte = [0u8; 1];
        reader.read_exact(&mut byte)?;
        let b = byte[0];

        result |= ((b & 0x7F) as u64) << shift;

        if (b & 0x80) == 0 {
            break;
        }

        shift += 7;
        if shift >= 64 {
            return Err(crate::error::TsFileError::Decoding(
                "VarInt too large".to_string(),
            ));
        }
    }

    Ok(result)
}

/// Write a variable-length signed integer
pub fn write_var_int<W: Write>(writer: &mut W, value: i64) -> Result<usize> {
    // ZigZag encoding to handle negative numbers efficiently
    let encoded = ((value << 1) ^ (value >> 63)) as u64;
    write_var_uint(writer, encoded)
}

/// Read a variable-length signed integer
pub fn read_var_int<R: Read>(reader: &mut R) -> Result<i64> {
    let encoded = read_var_uint(reader)?;
    // ZigZag decoding
    let value = ((encoded >> 1) as i64) ^ (-((encoded & 1) as i64));
    Ok(value)
}

/// Write a string with length prefix
pub fn write_var_string<W: Write>(writer: &mut W, s: &str) -> Result<usize> {
    let bytes = s.as_bytes();
    let mut total = write_var_uint(writer, bytes.len() as u64)?;
    writer.write_all(bytes)?;
    total += bytes.len();
    Ok(total)
}

/// Read a string with length prefix
pub fn read_var_string<R: Read>(reader: &mut R) -> Result<String> {
    let len = read_var_uint(reader)? as usize;
    let mut bytes = vec![0u8; len];
    reader.read_exact(&mut bytes)?;
    Ok(String::from_utf8(bytes).map_err(|e| {
        crate::error::TsFileError::Decoding(format!("Invalid UTF-8: {}", e))
    })?)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;

    #[test]
    fn test_var_uint() {
        let test_cases = vec![0u64, 1, 127, 128, 255, 256, 16383, 16384, u64::MAX];

        for &value in &test_cases {
            let mut buf = Vec::new();
            write_var_uint(&mut buf, value).unwrap();

            let mut cursor = Cursor::new(buf);
            let decoded = read_var_uint(&mut cursor).unwrap();
            assert_eq!(decoded, value, "Failed for value {}", value);
        }
    }

    #[test]
    fn test_var_int() {
        let test_cases = vec![0i64, 1, -1, 127, -127, 128, -128, 1000, -1000, i64::MAX, i64::MIN];

        for &value in &test_cases {
            let mut buf = Vec::new();
            write_var_int(&mut buf, value).unwrap();

            let mut cursor = Cursor::new(buf);
            let decoded = read_var_int(&mut cursor).unwrap();
            assert_eq!(decoded, value, "Failed for value {}", value);
        }
    }

    #[test]
    fn test_var_string() {
        let test_cases: Vec<String> = vec![
            "".to_string(),
            "hello".to_string(),
            "TsFile".to_string(),
            "测试字符串".to_string(),
            "a".repeat(1000),
        ];

        for s in &test_cases {
            let mut buf = Vec::new();
            write_var_string(&mut buf, s).unwrap();

            let mut cursor = Cursor::new(buf);
            let decoded = read_var_string(&mut cursor).unwrap();
            assert_eq!(&decoded, s, "Failed for string: {}", s);
        }
    }

    #[test]
    fn test_varint_size() {
        // Test that small values use 1 byte
        let mut buf = Vec::new();
        write_var_uint(&mut buf, 127).unwrap();
        assert_eq!(buf.len(), 1);

        // Test that 128 uses 2 bytes
        let mut buf = Vec::new();
        write_var_uint(&mut buf, 128).unwrap();
        assert_eq!(buf.len(), 2);
    }
}
