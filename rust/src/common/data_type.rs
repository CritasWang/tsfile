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

//! Data type definitions

use std::fmt;

/// Represents the data type of a measurement
///
/// This enumeration defines the supported data types for measurements in TsFile.
/// Compatible with C++ TSDataType enum.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(u8)]
pub enum TSDataType {
    /// Boolean type
    Boolean = 0,
    /// 32-bit signed integer
    Int32 = 1,
    /// 64-bit signed integer
    Int64 = 2,
    /// 32-bit floating point
    Float = 3,
    /// 64-bit floating point
    Double = 4,
    /// Text type (variable length string)
    Text = 5,
    /// Vector type
    Vector = 6,
    /// Unknown type
    Unknown = 7,
    /// Timestamp type (64-bit signed integer)
    Timestamp = 8,
    /// Date type
    Date = 9,
    /// Binary large object
    Blob = 10,
    /// String type (variable length string)
    String = 11,
    /// Null type
    Null = 254,
    /// Invalid data type
    Invalid = 255,
}

impl TSDataType {
    /// Get the name of the data type
    pub fn name(&self) -> &'static str {
        match self {
            TSDataType::Boolean => "BOOLEAN",
            TSDataType::Int32 => "INT32",
            TSDataType::Int64 => "INT64",
            TSDataType::Float => "FLOAT",
            TSDataType::Double => "DOUBLE",
            TSDataType::Text => "TEXT",
            TSDataType::Vector => "VECTOR",
            TSDataType::Unknown => "UNKNOWN",
            TSDataType::Timestamp => "TIMESTAMP",
            TSDataType::Date => "DATE",
            TSDataType::Blob => "BLOB",
            TSDataType::String => "STRING",
            TSDataType::Null => "NULL",
            TSDataType::Invalid => "INVALID",
        }
    }

    /// Get the size in bytes for fixed-size data types
    ///
    /// Returns None for variable-size data types (Text, String, Blob, Vector)
    pub fn size(&self) -> Option<usize> {
        match self {
            TSDataType::Boolean => Some(1),
            TSDataType::Int32 | TSDataType::Float | TSDataType::Date => Some(4),
            TSDataType::Int64 | TSDataType::Double | TSDataType::Timestamp => Some(8),
            _ => None,
        }
    }

    /// Check if this is a variable-size data type
    pub fn is_variable_size(&self) -> bool {
        matches!(
            self,
            TSDataType::Text | TSDataType::String | TSDataType::Blob | TSDataType::Vector
        )
    }

    /// Check if this is a numeric data type
    pub fn is_numeric(&self) -> bool {
        matches!(
            self,
            TSDataType::Int32
                | TSDataType::Int64
                | TSDataType::Float
                | TSDataType::Double
                | TSDataType::Timestamp
                | TSDataType::Date
        )
    }

    /// Parse from byte value
    pub fn from_byte(value: u8) -> Self {
        match value {
            0 => TSDataType::Boolean,
            1 => TSDataType::Int32,
            2 => TSDataType::Int64,
            3 => TSDataType::Float,
            4 => TSDataType::Double,
            5 => TSDataType::Text,
            6 => TSDataType::Vector,
            7 => TSDataType::Unknown,
            8 => TSDataType::Timestamp,
            9 => TSDataType::Date,
            10 => TSDataType::Blob,
            11 => TSDataType::String,
            254 => TSDataType::Null,
            _ => TSDataType::Invalid,
        }
    }

    /// Convert to byte value
    pub fn to_byte(&self) -> u8 {
        *self as u8
    }
}

impl fmt::Display for TSDataType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.name())
    }
}

impl TryFrom<u8> for TSDataType {
    type Error = crate::error::TsFileError;

    fn try_from(value: u8) -> Result<Self, Self::Error> {
        let dt = Self::from_byte(value);
        if matches!(dt, TSDataType::Invalid) {
            Err(crate::error::TsFileError::InvalidDataType(format!(
                "Invalid data type value: {}",
                value
            )))
        } else {
            Ok(dt)
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_data_type_names() {
        assert_eq!(TSDataType::Boolean.name(), "BOOLEAN");
        assert_eq!(TSDataType::Int32.name(), "INT32");
        assert_eq!(TSDataType::Int64.name(), "INT64");
        assert_eq!(TSDataType::String.name(), "STRING");
    }

    #[test]
    fn test_data_type_size() {
        assert_eq!(TSDataType::Boolean.size(), Some(1));
        assert_eq!(TSDataType::Int32.size(), Some(4));
        assert_eq!(TSDataType::Int64.size(), Some(8));
        assert_eq!(TSDataType::Double.size(), Some(8));
        assert_eq!(TSDataType::String.size(), None);
        assert_eq!(TSDataType::Text.size(), None);
    }

    #[test]
    fn test_is_variable_size() {
        assert!(!TSDataType::Boolean.is_variable_size());
        assert!(!TSDataType::Int64.is_variable_size());
        assert!(TSDataType::String.is_variable_size());
        assert!(TSDataType::Text.is_variable_size());
        assert!(TSDataType::Blob.is_variable_size());
    }

    #[test]
    fn test_is_numeric() {
        assert!(TSDataType::Int32.is_numeric());
        assert!(TSDataType::Int64.is_numeric());
        assert!(TSDataType::Float.is_numeric());
        assert!(TSDataType::Double.is_numeric());
        assert!(!TSDataType::Boolean.is_numeric());
        assert!(!TSDataType::String.is_numeric());
    }

    #[test]
    fn test_from_byte() {
        assert_eq!(TSDataType::from_byte(0), TSDataType::Boolean);
        assert_eq!(TSDataType::from_byte(1), TSDataType::Int32);
        assert_eq!(TSDataType::from_byte(11), TSDataType::String);
        assert_eq!(TSDataType::from_byte(255), TSDataType::Invalid);
    }

    #[test]
    fn test_to_byte() {
        assert_eq!(TSDataType::Boolean.to_byte(), 0);
        assert_eq!(TSDataType::Int32.to_byte(), 1);
        assert_eq!(TSDataType::String.to_byte(), 11);
    }

    #[test]
    fn test_try_from() {
        assert!(TSDataType::try_from(0).is_ok());
        assert!(TSDataType::try_from(1).is_ok());
        assert!(TSDataType::try_from(255).is_err());
    }
}
