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

//! Encoding type definitions

use std::fmt;

/// Represents the encoding method for a measurement
///
/// This enumeration defines the supported encoding methods that can be applied
/// to measurements. Compatible with C++ TSEncoding enum.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(u8)]
pub enum TSEncoding {
    /// Plain encoding (no encoding)
    Plain = 0,
    /// Dictionary encoding
    Dictionary = 1,
    /// Run-length encoding
    RLE = 2,
    /// Differential encoding
    Diff = 3,
    /// Two-level differential encoding
    TS2Diff = 4,
    /// Bitmap encoding
    Bitmap = 5,
    /// Gorilla V1 encoding
    GorillaV1 = 6,
    /// Regular encoding
    Regular = 7,
    /// Gorilla encoding (optimized for floating point)
    Gorilla = 8,
    /// Zigzag encoding
    Zigzag = 9,
    /// Frequency encoding
    Freq = 10,
    /// Sprintz encoding
    Sprintz = 12,
    /// Invalid encoding
    Invalid = 255,
}

impl TSEncoding {
    /// Get the name of the encoding
    pub fn name(&self) -> &'static str {
        match self {
            TSEncoding::Plain => "PLAIN",
            TSEncoding::Dictionary => "DICTIONARY",
            TSEncoding::RLE => "RLE",
            TSEncoding::Diff => "DIFF",
            TSEncoding::TS2Diff => "TS_2DIFF",
            TSEncoding::Bitmap => "BITMAP",
            TSEncoding::GorillaV1 => "GORILLA_V1",
            TSEncoding::Regular => "REGULAR",
            TSEncoding::Gorilla => "GORILLA",
            TSEncoding::Zigzag => "ZIGZAG",
            TSEncoding::Freq => "FREQ",
            TSEncoding::Sprintz => "SPRINTZ",
            TSEncoding::Invalid => "INVALID",
        }
    }

    /// Parse from byte value
    pub fn from_byte(value: u8) -> Self {
        match value {
            0 => TSEncoding::Plain,
            1 => TSEncoding::Dictionary,
            2 => TSEncoding::RLE,
            3 => TSEncoding::Diff,
            4 => TSEncoding::TS2Diff,
            5 => TSEncoding::Bitmap,
            6 => TSEncoding::GorillaV1,
            7 => TSEncoding::Regular,
            8 => TSEncoding::Gorilla,
            9 => TSEncoding::Zigzag,
            10 => TSEncoding::Freq,
            12 => TSEncoding::Sprintz,
            _ => TSEncoding::Invalid,
        }
    }

    /// Convert to byte value
    pub fn to_byte(&self) -> u8 {
        *self as u8
    }
}

impl fmt::Display for TSEncoding {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.name())
    }
}

impl TryFrom<u8> for TSEncoding {
    type Error = crate::error::TsFileError;

    fn try_from(value: u8) -> Result<Self, Self::Error> {
        let enc = Self::from_byte(value);
        if matches!(enc, TSEncoding::Invalid) {
            Err(crate::error::TsFileError::InvalidEncoding(format!(
                "Invalid encoding value: {}",
                value
            )))
        } else {
            Ok(enc)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_encoding_names() {
        assert_eq!(TSEncoding::Plain.name(), "PLAIN");
        assert_eq!(TSEncoding::RLE.name(), "RLE");
        assert_eq!(TSEncoding::Gorilla.name(), "GORILLA");
        assert_eq!(TSEncoding::TS2Diff.name(), "TS_2DIFF");
    }

    #[test]
    fn test_from_byte() {
        assert_eq!(TSEncoding::from_byte(0), TSEncoding::Plain);
        assert_eq!(TSEncoding::from_byte(2), TSEncoding::RLE);
        assert_eq!(TSEncoding::from_byte(8), TSEncoding::Gorilla);
        assert_eq!(TSEncoding::from_byte(255), TSEncoding::Invalid);
    }

    #[test]
    fn test_to_byte() {
        assert_eq!(TSEncoding::Plain.to_byte(), 0);
        assert_eq!(TSEncoding::RLE.to_byte(), 2);
        assert_eq!(TSEncoding::Gorilla.to_byte(), 8);
    }

    #[test]
    fn test_try_from() {
        assert!(TSEncoding::try_from(0).is_ok());
        assert!(TSEncoding::try_from(8).is_ok());
        assert!(TSEncoding::try_from(255).is_err());
    }
}
