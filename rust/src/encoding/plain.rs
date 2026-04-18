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

//! Plain encoding (no encoding, raw binary)

use crate::error::Result;
use byteorder::{LittleEndian, WriteBytesExt, ReadBytesExt};
use std::io::{Write, Read};

/// Plain encoder - writes data in raw binary format
pub struct PlainEncoder;

impl PlainEncoder {
    /// Encode a boolean value
    pub fn encode_bool<W: Write>(writer: &mut W, value: bool) -> Result<()> {
        writer.write_u8(value as u8)?;
        Ok(())
    }

    /// Encode an i32 value
    pub fn encode_i32<W: Write>(writer: &mut W, value: i32) -> Result<()> {
        writer.write_i32::<LittleEndian>(value)?;
        Ok(())
    }

    /// Encode an i64 value
    pub fn encode_i64<W: Write>(writer: &mut W, value: i64) -> Result<()> {
        writer.write_i64::<LittleEndian>(value)?;
        Ok(())
    }

    /// Encode an f32 value
    pub fn encode_f32<W: Write>(writer: &mut W, value: f32) -> Result<()> {
        writer.write_f32::<LittleEndian>(value)?;
        Ok(())
    }

    /// Encode an f64 value
    pub fn encode_f64<W: Write>(writer: &mut W, value: f64) -> Result<()> {
        writer.write_f64::<LittleEndian>(value)?;
        Ok(())
    }

    /// Encode a string value
    pub fn encode_string<W: Write>(writer: &mut W, value: &str) -> Result<()> {
        let bytes = value.as_bytes();
        writer.write_i32::<LittleEndian>(bytes.len() as i32)?;
        writer.write_all(bytes)?;
        Ok(())
    }

    /// Decode a boolean value
    pub fn decode_bool<R: Read>(reader: &mut R) -> Result<bool> {
        Ok(reader.read_u8()? != 0)
    }

    /// Decode an i32 value
    pub fn decode_i32<R: Read>(reader: &mut R) -> Result<i32> {
        Ok(reader.read_i32::<LittleEndian>()?)
    }

    /// Decode an i64 value
    pub fn decode_i64<R: Read>(reader: &mut R) -> Result<i64> {
        Ok(reader.read_i64::<LittleEndian>()?)
    }

    /// Decode an f32 value
    pub fn decode_f32<R: Read>(reader: &mut R) -> Result<f32> {
        Ok(reader.read_f32::<LittleEndian>()?)
    }

    /// Decode an f64 value
    pub fn decode_f64<R: Read>(reader: &mut R) -> Result<f64> {
        Ok(reader.read_f64::<LittleEndian>()?)
    }

    /// Decode a string value
    pub fn decode_string<R: Read>(reader: &mut R) -> Result<String> {
        let len = reader.read_i32::<LittleEndian>()? as usize;
        let mut bytes = vec![0u8; len];
        reader.read_exact(&mut bytes)?;
        Ok(String::from_utf8(bytes).map_err(|e| {
            crate::error::TsFileError::Decoding(format!("Invalid UTF-8: {}", e))
        })?)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;

    #[test]
    fn test_encode_decode_bool() {
        let mut buf = Vec::new();
        PlainEncoder::encode_bool(&mut buf, true).unwrap();
        PlainEncoder::encode_bool(&mut buf, false).unwrap();

        let mut cursor = Cursor::new(buf);
        assert_eq!(PlainEncoder::decode_bool(&mut cursor).unwrap(), true);
        assert_eq!(PlainEncoder::decode_bool(&mut cursor).unwrap(), false);
    }

    #[test]
    fn test_encode_decode_numbers() {
        let mut buf = Vec::new();
        PlainEncoder::encode_i32(&mut buf, 42).unwrap();
        PlainEncoder::encode_i64(&mut buf, 1234567890).unwrap();
        PlainEncoder::encode_f32(&mut buf, 3.14).unwrap();
        PlainEncoder::encode_f64(&mut buf, 2.718281828).unwrap();

        let mut cursor = Cursor::new(buf);
        assert_eq!(PlainEncoder::decode_i32(&mut cursor).unwrap(), 42);
        assert_eq!(PlainEncoder::decode_i64(&mut cursor).unwrap(), 1234567890);
        assert!((PlainEncoder::decode_f32(&mut cursor).unwrap() - 3.14).abs() < 0.001);
        assert!((PlainEncoder::decode_f64(&mut cursor).unwrap() - 2.718281828).abs() < 0.000001);
    }

    #[test]
    fn test_encode_decode_string() {
        let mut buf = Vec::new();
        PlainEncoder::encode_string(&mut buf, "Hello, TsFile!").unwrap();

        let mut cursor = Cursor::new(buf);
        assert_eq!(PlainEncoder::decode_string(&mut cursor).unwrap(), "Hello, TsFile!");
    }
}
