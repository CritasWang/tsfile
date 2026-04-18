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

//! Compression implementations

use crate::common::CompressionType;
use crate::error::{Result, TsFileError};

/// Compress data using the specified compression type
pub fn compress(data: &[u8], compression: CompressionType) -> Result<Vec<u8>> {
    match compression {
        CompressionType::Uncompressed => Ok(data.to_vec()),

        #[cfg(feature = "snappy")]
        CompressionType::Snappy => {
            let mut encoder = snap::raw::Encoder::new();
            encoder.compress_vec(data).map_err(|e| {
                TsFileError::Compression(format!("Snappy compression failed: {}", e))
            })
        }

        #[cfg(feature = "gzip")]
        CompressionType::Gzip => {
            use flate2::write::GzEncoder;
            use flate2::Compression;
            use std::io::Write;

            let mut encoder = GzEncoder::new(Vec::new(), Compression::default());
            encoder.write_all(data)?;
            encoder.finish().map_err(|e| {
                TsFileError::Compression(format!("Gzip compression failed: {}", e))
            })
        }

        #[cfg(feature = "lz4")]
        CompressionType::LZ4 => {
            lz4::block::compress(data, None, false).map_err(|e| {
                TsFileError::Compression(format!("LZ4 compression failed: {}", e))
            })
        }

        _ => Err(TsFileError::FeatureNotEnabled(format!(
            "Compression type {} is not enabled or supported",
            compression.name()
        ))),
    }
}

/// Decompress data using the specified compression type
pub fn decompress(data: &[u8], compression: CompressionType, original_size: Option<usize>) -> Result<Vec<u8>> {
    match compression {
        CompressionType::Uncompressed => Ok(data.to_vec()),

        #[cfg(feature = "snappy")]
        CompressionType::Snappy => {
            let mut decoder = snap::raw::Decoder::new();
            decoder.decompress_vec(data).map_err(|e| {
                TsFileError::Decompression(format!("Snappy decompression failed: {}", e))
            })
        }

        #[cfg(feature = "gzip")]
        CompressionType::Gzip => {
            use flate2::read::GzDecoder;
            use std::io::Read;

            let mut decoder = GzDecoder::new(data);
            let mut result = Vec::new();
            decoder.read_to_end(&mut result)?;
            Ok(result)
        }

        #[cfg(feature = "lz4")]
        CompressionType::LZ4 => {
            let size = original_size.ok_or_else(|| {
                TsFileError::Decompression("LZ4 decompression requires original size".to_string())
            })?;
            lz4::block::decompress(data, Some(size as i32)).map_err(|e| {
                TsFileError::Decompression(format!("LZ4 decompression failed: {}", e))
            })
        }

        _ => Err(TsFileError::FeatureNotEnabled(format!(
            "Compression type {} is not enabled or supported",
            compression.name()
        ))),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_uncompressed() {
        let data = b"Hello, TsFile!";
        let compressed = compress(data, CompressionType::Uncompressed).unwrap();
        assert_eq!(compressed, data);
        let decompressed = decompress(&compressed, CompressionType::Uncompressed, None).unwrap();
        assert_eq!(decompressed, data);
    }

    #[cfg(feature = "snappy")]
    #[test]
    fn test_snappy() {
        let data = b"Hello, TsFile! This is a test of Snappy compression.".repeat(10);
        let compressed = compress(&data, CompressionType::Snappy).unwrap();
        assert!(compressed.len() < data.len());
        let decompressed = decompress(&compressed, CompressionType::Snappy, None).unwrap();
        assert_eq!(decompressed, data);
    }

    #[cfg(feature = "gzip")]
    #[test]
    fn test_gzip() {
        let data = b"Hello, TsFile! This is a test of Gzip compression.".repeat(10);
        let compressed = compress(&data, CompressionType::Gzip).unwrap();
        assert!(compressed.len() < data.len());
        let decompressed = decompress(&compressed, CompressionType::Gzip, None).unwrap();
        assert_eq!(decompressed, data);
    }

    #[cfg(feature = "lz4")]
    #[test]
    fn test_lz4() {
        let data = b"Hello, TsFile! This is a test of LZ4 compression.".repeat(10);
        let original_size = data.len();
        let compressed = compress(&data, CompressionType::LZ4).unwrap();
        let decompressed = decompress(&compressed, CompressionType::LZ4, Some(original_size)).unwrap();
        assert_eq!(decompressed, data);
    }
}
