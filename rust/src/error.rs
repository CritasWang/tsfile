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

//! Error types for TsFile operations

use std::io;
use thiserror::Error;

/// Result type for TsFile operations
pub type Result<T> = std::result::Result<T, TsFileError>;

/// Errors that can occur when working with TsFile
#[derive(Error, Debug)]
pub enum TsFileError {
    /// I/O errors
    #[error("I/O error: {0}")]
    Io(#[from] io::Error),

    /// Invalid data type
    #[error("Invalid data type: {0}")]
    InvalidDataType(String),

    /// Invalid encoding
    #[error("Invalid encoding: {0}")]
    InvalidEncoding(String),

    /// Invalid compression type
    #[error("Invalid compression type: {0}")]
    InvalidCompression(String),

    /// Schema error
    #[error("Schema error: {0}")]
    Schema(String),

    /// Write error
    #[error("Write error: {0}")]
    Write(String),

    /// Read error
    #[error("Read error: {0}")]
    Read(String),

    /// Encoding error
    #[error("Encoding error: {0}")]
    Encoding(String),

    /// Decoding error
    #[error("Decoding error: {0}")]
    Decoding(String),

    /// Compression error
    #[error("Compression error: {0}")]
    Compression(String),

    /// Decompression error
    #[error("Decompression error: {0}")]
    Decompression(String),

    /// File format error
    #[error("File format error: {0}")]
    Format(String),

    /// Invalid query
    #[error("Invalid query: {0}")]
    Query(String),

    /// Column not found
    #[error("Column not found: {0}")]
    ColumnNotFound(String),

    /// Invalid column index
    #[error("Invalid column index: {0}")]
    InvalidColumnIndex(usize),

    /// Type mismatch
    #[error("Type mismatch: expected {expected}, got {actual}")]
    TypeMismatch { expected: String, actual: String },

    /// Out of bounds
    #[error("Out of bounds: index {index}, size {size}")]
    OutOfBounds { index: usize, size: usize },

    /// Feature not enabled
    #[error("Feature not enabled: {0}")]
    FeatureNotEnabled(String),

    /// Not implemented
    #[error("Not implemented: {0}")]
    NotImplemented(String),

    /// Other error
    #[error("{0}")]
    Other(String),
}

impl TsFileError {
    /// Create a new schema error
    pub fn schema<S: Into<String>>(msg: S) -> Self {
        TsFileError::Schema(msg.into())
    }

    /// Create a new write error
    pub fn write<S: Into<String>>(msg: S) -> Self {
        TsFileError::Write(msg.into())
    }

    /// Create a new read error
    pub fn read<S: Into<String>>(msg: S) -> Self {
        TsFileError::Read(msg.into())
    }

    /// Create a new format error
    pub fn format<S: Into<String>>(msg: S) -> Self {
        TsFileError::Format(msg.into())
    }

    /// Create a new query error
    pub fn query<S: Into<String>>(msg: S) -> Self {
        TsFileError::Query(msg.into())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_error_display() {
        let err = TsFileError::schema("test schema error");
        assert_eq!(err.to_string(), "Schema error: test schema error");

        let err = TsFileError::TypeMismatch {
            expected: "INT64".to_string(),
            actual: "STRING".to_string(),
        };
        assert_eq!(err.to_string(), "Type mismatch: expected INT64, got STRING");
    }

    #[test]
    fn test_error_from_io() {
        let io_err = io::Error::new(io::ErrorKind::NotFound, "file not found");
        let ts_err: TsFileError = io_err.into();
        assert!(matches!(ts_err, TsFileError::Io(_)));
    }
}
