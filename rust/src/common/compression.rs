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

//! Compression type definitions

use std::fmt;

/// Represents the compression type for a measurement
///
/// This enumeration defines the supported compression methods that can be
/// applied to measurements. Compatible with C++ CompressionType enum.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(u8)]
pub enum CompressionType {
    /// No compression
    Uncompressed = 0,
    /// Snappy compression
    Snappy = 1,
    /// Gzip compression
    Gzip = 2,
    /// LZO compression
    LZO = 3,
    /// SDT compression
    SDT = 4,
    /// PAA compression
    PAA = 5,
    /// PLA compression
    PLA = 6,
    /// LZ4 compression
    LZ4 = 7,
    /// Invalid compression type
    Invalid = 255,
}

impl CompressionType {
    /// Get the name of the compression type
    pub fn name(&self) -> &'static str {
        match self {
            CompressionType::Uncompressed => "UNCOMPRESSED",
            CompressionType::Snappy => "SNAPPY",
            CompressionType::Gzip => "GZIP",
            CompressionType::LZO => "LZO",
            CompressionType::SDT => "SDT",
            CompressionType::PAA => "PAA",
            CompressionType::PLA => "PLA",
            CompressionType::LZ4 => "LZ4",
            CompressionType::Invalid => "INVALID",
        }
    }

    /// Parse from byte value
    pub fn from_byte(value: u8) -> Self {
        match value {
            0 => CompressionType::Uncompressed,
            1 => CompressionType::Snappy,
            2 => CompressionType::Gzip,
            3 => CompressionType::LZO,
            4 => CompressionType::SDT,
            5 => CompressionType::PAA,
            6 => CompressionType::PLA,
            7 => CompressionType::LZ4,
            _ => CompressionType::Invalid,
        }
    }

    /// Convert to byte value
    pub fn to_byte(&self) -> u8 {
        *self as u8
    }

    /// Check if this compression type is supported by the current build
    #[allow(unreachable_code)]
    pub fn is_supported(&self) -> bool {
        match self {
            CompressionType::Uncompressed => true,
            #[cfg(feature = "snappy")]
            CompressionType::Snappy => return true,
            #[cfg(feature = "gzip")]
            CompressionType::Gzip => return true,
            #[cfg(feature = "lz4")]
            CompressionType::LZ4 => return true,
            CompressionType::LZO | CompressionType::SDT | CompressionType::PAA | CompressionType::PLA => false,
            _ => false,
        }
    }
}

impl fmt::Display for CompressionType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.name())
    }
}

impl TryFrom<u8> for CompressionType {
    type Error = crate::error::TsFileError;

    fn try_from(value: u8) -> Result<Self, Self::Error> {
        let ct = Self::from_byte(value);
        if matches!(ct, CompressionType::Invalid) {
            Err(crate::error::TsFileError::InvalidCompression(format!(
                "Invalid compression type value: {}",
                value
            )))
        } else {
            Ok(ct)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_compression_names() {
        assert_eq!(CompressionType::Uncompressed.name(), "UNCOMPRESSED");
        assert_eq!(CompressionType::Snappy.name(), "SNAPPY");
        assert_eq!(CompressionType::Gzip.name(), "GZIP");
        assert_eq!(CompressionType::LZ4.name(), "LZ4");
    }

    #[test]
    fn test_from_byte() {
        assert_eq!(CompressionType::from_byte(0), CompressionType::Uncompressed);
        assert_eq!(CompressionType::from_byte(1), CompressionType::Snappy);
        assert_eq!(CompressionType::from_byte(7), CompressionType::LZ4);
        assert_eq!(CompressionType::from_byte(255), CompressionType::Invalid);
    }

    #[test]
    fn test_to_byte() {
        assert_eq!(CompressionType::Uncompressed.to_byte(), 0);
        assert_eq!(CompressionType::Snappy.to_byte(), 1);
        assert_eq!(CompressionType::LZ4.to_byte(), 7);
    }

    #[test]
    fn test_is_supported() {
        assert!(CompressionType::Uncompressed.is_supported());
        // These depend on features, but test the method works
        let _ = CompressionType::Snappy.is_supported();
        let _ = CompressionType::Gzip.is_supported();
        let _ = CompressionType::LZ4.is_supported();
    }

    #[test]
    fn test_try_from() {
        assert!(CompressionType::try_from(0).is_ok());
        assert!(CompressionType::try_from(1).is_ok());
        assert!(CompressionType::try_from(255).is_err());
    }
}
