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

//! Schema definitions for TsFile

use super::{CompressionType, TSDataType, TSEncoding};
use std::fmt;

/// Column category - distinguishes between tags and fields
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(u8)]
pub enum ColumnCategory {
    /// Tag column (dimension/identifier)
    Tag = 0,
    /// Field column (measurement value)
    Field = 1,
}

impl ColumnCategory {
    /// Parse from byte value
    pub fn from_byte(value: u8) -> Option<Self> {
        match value {
            0 => Some(ColumnCategory::Tag),
            1 => Some(ColumnCategory::Field),
            _ => None,
        }
    }

    /// Convert to byte value
    pub fn to_byte(&self) -> u8 {
        *self as u8
    }
}

impl fmt::Display for ColumnCategory {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ColumnCategory::Tag => write!(f, "TAG"),
            ColumnCategory::Field => write!(f, "FIELD"),
        }
    }
}

/// Schema for a column in a table
#[derive(Debug, Clone)]
pub struct ColumnSchema {
    /// Column name
    pub column_name: String,
    /// Data type
    pub data_type: TSDataType,
    /// Compression type
    pub compression: CompressionType,
    /// Encoding type
    pub encoding: TSEncoding,
    /// Column category (tag or field)
    pub category: ColumnCategory,
}

impl ColumnSchema {
    /// Create a new column schema
    pub fn new(
        column_name: impl Into<String>,
        data_type: TSDataType,
        compression: CompressionType,
        encoding: TSEncoding,
        category: ColumnCategory,
    ) -> Self {
        Self {
            column_name: column_name.into(),
            data_type,
            compression,
            encoding,
            category,
        }
    }

    /// Create a tag column with default settings
    pub fn tag(column_name: impl Into<String>, data_type: TSDataType) -> Self {
        Self::new(
            column_name,
            data_type,
            CompressionType::Uncompressed,
            TSEncoding::Plain,
            ColumnCategory::Tag,
        )
    }

    /// Create a field column with default settings
    pub fn field(column_name: impl Into<String>, data_type: TSDataType) -> Self {
        Self::new(
            column_name,
            data_type,
            CompressionType::Uncompressed,
            TSEncoding::Plain,
            ColumnCategory::Field,
        )
    }

    /// Check if this is a tag column
    pub fn is_tag(&self) -> bool {
        matches!(self.category, ColumnCategory::Tag)
    }

    /// Check if this is a field column
    pub fn is_field(&self) -> bool {
        matches!(self.category, ColumnCategory::Field)
    }
}

/// Schema for a table in TsFile
#[derive(Debug, Clone)]
pub struct TableSchema {
    /// Table name
    pub table_name: String,
    /// Column schemas
    pub columns: Vec<ColumnSchema>,
}

impl TableSchema {
    /// Create a new table schema
    pub fn new(table_name: impl Into<String>, columns: Vec<ColumnSchema>) -> Self {
        Self {
            table_name: table_name.into(),
            columns,
        }
    }

    /// Get the number of columns
    pub fn column_count(&self) -> usize {
        self.columns.len()
    }

    /// Get a column by index
    pub fn get_column(&self, index: usize) -> Option<&ColumnSchema> {
        self.columns.get(index)
    }

    /// Find a column by name
    pub fn find_column(&self, name: &str) -> Option<&ColumnSchema> {
        self.columns.iter().find(|c| c.column_name == name)
    }

    /// Get the index of a column by name
    pub fn column_index(&self, name: &str) -> Option<usize> {
        self.columns.iter().position(|c| c.column_name == name)
    }

    /// Get all tag columns
    pub fn tag_columns(&self) -> Vec<&ColumnSchema> {
        self.columns.iter().filter(|c| c.is_tag()).collect()
    }

    /// Get all field columns
    pub fn field_columns(&self) -> Vec<&ColumnSchema> {
        self.columns.iter().filter(|c| c.is_field()).collect()
    }
}

/// Schema for a measurement (legacy, for compatibility with timeseries model)
#[derive(Debug, Clone)]
pub struct MeasurementSchema {
    /// Measurement name
    pub measurement_name: String,
    /// Data type
    pub data_type: TSDataType,
    /// Encoding type
    pub encoding: TSEncoding,
    /// Compression type
    pub compression: CompressionType,
}

impl MeasurementSchema {
    /// Create a new measurement schema
    pub fn new(
        measurement_name: impl Into<String>,
        data_type: TSDataType,
        encoding: TSEncoding,
        compression: CompressionType,
    ) -> Self {
        Self {
            measurement_name: measurement_name.into(),
            data_type,
            encoding,
            compression,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_column_category() {
        assert_eq!(ColumnCategory::Tag.to_byte(), 0);
        assert_eq!(ColumnCategory::Field.to_byte(), 1);
        assert_eq!(ColumnCategory::from_byte(0), Some(ColumnCategory::Tag));
        assert_eq!(ColumnCategory::from_byte(1), Some(ColumnCategory::Field));
        assert_eq!(ColumnCategory::from_byte(2), None);
    }

    #[test]
    fn test_column_schema() {
        let schema = ColumnSchema::new(
            "test_col",
            TSDataType::Int64,
            CompressionType::Snappy,
            TSEncoding::Plain,
            ColumnCategory::Field,
        );
        assert_eq!(schema.column_name, "test_col");
        assert_eq!(schema.data_type, TSDataType::Int64);
        assert!(schema.is_field());
        assert!(!schema.is_tag());

        let tag_schema = ColumnSchema::tag("tag1", TSDataType::String);
        assert!(tag_schema.is_tag());
        assert_eq!(tag_schema.encoding, TSEncoding::Plain);
    }

    #[test]
    fn test_table_schema() {
        let schema = TableSchema::new(
            "test_table",
            vec![
                ColumnSchema::tag("id1", TSDataType::String),
                ColumnSchema::field("s1", TSDataType::Int64),
                ColumnSchema::field("s2", TSDataType::Double),
            ],
        );

        assert_eq!(schema.table_name, "test_table");
        assert_eq!(schema.column_count(), 3);
        assert_eq!(schema.tag_columns().len(), 1);
        assert_eq!(schema.field_columns().len(), 2);
        assert_eq!(schema.column_index("s1"), Some(1));
        assert_eq!(schema.column_index("unknown"), None);
        assert!(schema.find_column("id1").is_some());
    }

    #[test]
    fn test_measurement_schema() {
        let schema = MeasurementSchema::new(
            "temperature",
            TSDataType::Float,
            TSEncoding::Gorilla,
            CompressionType::Snappy,
        );
        assert_eq!(schema.measurement_name, "temperature");
        assert_eq!(schema.data_type, TSDataType::Float);
    }
}
