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

//! Gorilla encoding for floating-point values
//!
//! Gorilla encoding is optimized for time series floating-point data.
//! It uses XOR-based delta encoding and bit packing to achieve excellent
//! compression for slowly changing values.
//!
//! Reference: "Gorilla: A Fast, Scalable, In-Memory Time Series Database"
//! by Pelkonen et al., Facebook, 2015

use crate::error::Result;
use byteorder::{LittleEndian, ReadBytesExt, WriteBytesExt};
use std::io::{Read, Write};

/// Gorilla encoder for f32 and f64 values
pub struct GorillaEncoder;

impl GorillaEncoder {
    /// Encode f32 values using Gorilla encoding
    pub fn encode_f32_array<W: Write>(writer: &mut W, values: &[f32]) -> Result<()> {
        if values.is_empty() {
            return Ok(());
        }

        // Write first value as-is
        writer.write_f32::<LittleEndian>(values[0])?;

        if values.len() == 1 {
            return Ok(());
        }

        // Initialize for XOR encoding
        let mut prev_value = values[0].to_bits();
        let mut prev_xor: u32 = 0;
        let mut prev_leading_zeros: u8 = 0;
        let mut prev_trailing_zeros: u8 = 0;

        let mut bit_writer = BitWriter::new();

        for &value in &values[1..] {
            let current_bits = value.to_bits();
            let xor = current_bits ^ prev_value;

            if xor == 0 {
                // Value unchanged, write single 0 bit
                bit_writer.write_bit(0)?;
            } else {
                // Value changed, write 1 bit
                bit_writer.write_bit(1)?;

                let leading_zeros = xor.leading_zeros() as u8;
                let trailing_zeros = xor.trailing_zeros() as u8;

                if leading_zeros >= prev_leading_zeros
                    && trailing_zeros >= prev_trailing_zeros
                    && prev_xor != 0
                {
                    // Use same block as previous
                    bit_writer.write_bit(0)?;
                    let meaningful_bits = 32 - prev_leading_zeros - prev_trailing_zeros;
                    let shifted_xor = (xor >> prev_trailing_zeros) & ((1 << meaningful_bits) - 1);
                    bit_writer.write_bits(shifted_xor as u64, meaningful_bits)?;
                } else {
                    // New block
                    bit_writer.write_bit(1)?;
                    bit_writer.write_bits(leading_zeros as u64, 5)?; // 5 bits for leading zeros
                    let meaningful_bits = 32 - leading_zeros - trailing_zeros;
                    bit_writer.write_bits(meaningful_bits as u64, 6)?; // 6 bits for block size
                    let shifted_xor = (xor >> trailing_zeros) & ((1 << meaningful_bits) - 1);
                    bit_writer.write_bits(shifted_xor as u64, meaningful_bits)?;

                    prev_leading_zeros = leading_zeros;
                    prev_trailing_zeros = trailing_zeros;
                }

                prev_xor = xor;
            }

            prev_value = current_bits;
        }

        // Flush remaining bits
        writer.write_all(&bit_writer.finish())?;

        Ok(())
    }

    /// Encode f64 values using Gorilla encoding
    pub fn encode_f64_array<W: Write>(writer: &mut W, values: &[f64]) -> Result<()> {
        if values.is_empty() {
            return Ok(());
        }

        // Write first value as-is
        writer.write_f64::<LittleEndian>(values[0])?;

        if values.len() == 1 {
            return Ok(());
        }

        // Initialize for XOR encoding
        let mut prev_value = values[0].to_bits();
        let mut prev_xor: u64 = 0;
        let mut prev_leading_zeros: u8 = 0;
        let mut prev_trailing_zeros: u8 = 0;

        let mut bit_writer = BitWriter::new();

        for &value in &values[1..] {
            let current_bits = value.to_bits();
            let xor = current_bits ^ prev_value;

            if xor == 0 {
                // Value unchanged, write single 0 bit
                bit_writer.write_bit(0)?;
            } else {
                // Value changed, write 1 bit
                bit_writer.write_bit(1)?;

                let leading_zeros = xor.leading_zeros() as u8;
                let trailing_zeros = xor.trailing_zeros() as u8;

                if leading_zeros >= prev_leading_zeros
                    && trailing_zeros >= prev_trailing_zeros
                    && prev_xor != 0
                {
                    // Use same block as previous
                    bit_writer.write_bit(0)?;
                    let meaningful_bits = 64 - prev_leading_zeros - prev_trailing_zeros;
                    let shifted_xor = (xor >> prev_trailing_zeros) & ((1 << meaningful_bits) - 1);
                    bit_writer.write_bits(shifted_xor, meaningful_bits)?;
                } else {
                    // New block
                    bit_writer.write_bit(1)?;
                    bit_writer.write_bits(leading_zeros as u64, 6)?; // 6 bits for leading zeros
                    let meaningful_bits = 64 - leading_zeros - trailing_zeros;
                    bit_writer.write_bits(meaningful_bits as u64, 6)?; // 6 bits for block size
                    let shifted_xor = (xor >> trailing_zeros) & ((1 << meaningful_bits) - 1);
                    bit_writer.write_bits(shifted_xor, meaningful_bits)?;

                    prev_leading_zeros = leading_zeros;
                    prev_trailing_zeros = trailing_zeros;
                }

                prev_xor = xor;
            }

            prev_value = current_bits;
        }

        // Flush remaining bits
        writer.write_all(&bit_writer.finish())?;

        Ok(())
    }
}

/// Simple bit-level writer
struct BitWriter {
    buffer: Vec<u8>,
    current_byte: u8,
    bit_position: u8,
}

impl BitWriter {
    fn new() -> Self {
        Self {
            buffer: Vec::new(),
            current_byte: 0,
            bit_position: 0,
        }
    }

    fn write_bit(&mut self, bit: u8) -> Result<()> {
        if bit != 0 {
            self.current_byte |= 1 << (7 - self.bit_position);
        }
        self.bit_position += 1;

        if self.bit_position == 8 {
            self.buffer.push(self.current_byte);
            self.current_byte = 0;
            self.bit_position = 0;
        }

        Ok(())
    }

    fn write_bits(&mut self, value: u64, num_bits: u8) -> Result<()> {
        for i in (0..num_bits).rev() {
            let bit = ((value >> i) & 1) as u8;
            self.write_bit(bit)?;
        }
        Ok(())
    }

    fn finish(mut self) -> Vec<u8> {
        if self.bit_position > 0 {
            self.buffer.push(self.current_byte);
        }
        self.buffer
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Cursor;

    #[test]
    fn test_gorilla_f32_constant() {
        let values = vec![1.5f32; 100];
        let mut buf = Vec::new();
        GorillaEncoder::encode_f32_array(&mut buf, &values).unwrap();

        // Should compress well - mostly 0 bits
        // First value (4 bytes) + compressed data (should be very small)
        println!("Compressed size: {} bytes for 100 constant values", buf.len());
        assert!(buf.len() < 100 * 4); // Much smaller than uncompressed
    }

    #[test]
    fn test_gorilla_f32_slowly_changing() {
        let values: Vec<f32> = (0..100).map(|i| 20.0 + (i as f32) * 0.1).collect();
        let mut buf = Vec::new();
        GorillaEncoder::encode_f32_array(&mut buf, &values).unwrap();

        println!("Compressed size: {} bytes for 100 slowly changing values", buf.len());
        // Should still compress reasonably well
        assert!(buf.len() < 100 * 4);
    }

    #[test]
    fn test_gorilla_f64_constant() {
        let values = vec![3.14159265359f64; 50];
        let mut buf = Vec::new();
        GorillaEncoder::encode_f64_array(&mut buf, &values).unwrap();

        println!("Compressed size: {} bytes for 50 constant f64 values", buf.len());
        assert!(buf.len() < 50 * 8); // Much smaller than uncompressed
    }

    #[test]
    fn test_gorilla_empty() {
        let values: Vec<f32> = vec![];
        let mut buf = Vec::new();
        GorillaEncoder::encode_f32_array(&mut buf, &values).unwrap();
        assert_eq!(buf.len(), 0);
    }

    #[test]
    fn test_gorilla_single_value() {
        let values = vec![42.0f32];
        let mut buf = Vec::new();
        GorillaEncoder::encode_f32_array(&mut buf, &values).unwrap();
        assert_eq!(buf.len(), 4); // Just the value itself
    }
}
