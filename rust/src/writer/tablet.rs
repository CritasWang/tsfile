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

//! Tablet data structure for batch operations

use crate::common::{TSDataType, Timestamp, ColumnCategory};
use crate::error::{Result, TsFileError};
use std::collections::HashMap;

/// Value type for a column
#[derive(Debug, Clone)]
pub enum ColumnValue {
    Boolean(Vec<Option<bool>>),
    Int32(Vec<Option<i32>>),
    Int64(Vec<Option<i64>>),
    Float(Vec<Option<f32>>),
    Double(Vec<Option<f64>>),
    String(Vec<Option<String>>),
}

impl ColumnValue {
    /// Create a new column value buffer with capacity
    pub fn new(data_type: TSDataType, capacity: usize) -> Self {
        match data_type {
            TSDataType::Boolean => ColumnValue::Boolean(Vec::with_capacity(capacity)),
            TSDataType::Int32 | TSDataType::Date => ColumnValue::Int32(Vec::with_capacity(capacity)),
            TSDataType::Int64 | TSDataType::Timestamp => ColumnValue::Int64(Vec::with_capacity(capacity)),
            TSDataType::Float => ColumnValue::Float(Vec::with_capacity(capacity)),
            TSDataType::Double => ColumnValue::Double(Vec::with_capacity(capacity)),
            TSDataType::String | TSDataType::Text | TSDataType::Blob => {
                ColumnValue::String(Vec::with_capacity(capacity))
            }
            _ => ColumnValue::String(Vec::with_capacity(capacity)),
        }
    }

    /// Get the number of values
    pub fn len(&self) -> usize {
        match self {
            ColumnValue::Boolean(v) => v.len(),
            ColumnValue::Int32(v) => v.len(),
            ColumnValue::Int64(v) => v.len(),
            ColumnValue::Float(v) => v.len(),
            ColumnValue::Double(v) => v.len(),
            ColumnValue::String(v) => v.len(),
        }
    }

    /// Check if empty
    pub fn is_empty(&self) -> bool {
        self.len() == 0
    }
}

/// Tablet for batch write operations
///
/// A tablet contains columnar data for a table, with timestamps and values
/// organized in column-major format for efficient batch operations.
pub struct Tablet {
    /// Table name
    pub table_name: String,
    /// Column names
    pub column_names: Vec<String>,
    /// Column data types
    pub column_types: Vec<TSDataType>,
    /// Column categories (tag or field)
    pub column_categories: Vec<ColumnCategory>,
    /// Timestamps
    pub timestamps: Vec<Timestamp>,
    /// Column values
    pub values: HashMap<String, ColumnValue>,
    /// Maximum row count
    pub max_rows: usize,
    /// Current row count
    pub row_count: usize,
}

impl Tablet {
    /// Create a new tablet
    pub fn new(
        table_name: impl Into<String>,
        column_names: Vec<impl Into<String>>,
        max_rows: usize,
    ) -> Self {
        let column_names: Vec<String> = column_names.into_iter().map(|s| s.into()).collect();
        Self {
            table_name: table_name.into(),
            column_names,
            column_types: Vec::new(),
            column_categories: Vec::new(),
            timestamps: Vec::with_capacity(max_rows),
            values: HashMap::new(),
            max_rows,
            row_count: 0,
        }
    }

    /// Create a new tablet with types and categories
    pub fn with_schema(
        table_name: impl Into<String>,
        column_names: Vec<impl Into<String>>,
        column_types: Vec<TSDataType>,
        column_categories: Vec<ColumnCategory>,
        max_rows: usize,
    ) -> Self {
        let column_names: Vec<String> = column_names.into_iter().map(|s| s.into()).collect();
        let mut values = HashMap::new();
        for (name, data_type) in column_names.iter().zip(column_types.iter()) {
            values.insert(name.clone(), ColumnValue::new(*data_type, max_rows));
        }

        Self {
            table_name: table_name.into(),
            column_names,
            column_types,
            column_categories,
            timestamps: Vec::with_capacity(max_rows),
            values,
            max_rows,
            row_count: 0,
        }
    }

    /// Add a timestamp
    pub fn add_timestamp(&mut self, _row: usize, timestamp: Timestamp) {
        self.timestamps.push(timestamp);
        self.row_count = self.timestamps.len();
    }

    /// Add a value to a column by name
    pub fn add_value<V: Into<TabletValue>>(&mut self, _row: usize, column: &str, value: V) -> Result<()> {
        let tablet_value = value.into();

        let col_values = self.values.get_mut(column)
            .ok_or_else(|| TsFileError::ColumnNotFound(column.to_string()))?;

        match tablet_value {
            TabletValue::Boolean(v) => {
                if let ColumnValue::Boolean(vals) = col_values {
                    vals.push(Some(v));
                } else {
                    return Err(TsFileError::TypeMismatch {
                        expected: "Boolean".to_string(),
                        actual: "different type".to_string(),
                    });
                }
            }
            TabletValue::Int32(v) => {
                if let ColumnValue::Int32(vals) = col_values {
                    vals.push(Some(v));
                } else {
                    return Err(TsFileError::TypeMismatch {
                        expected: "Int32".to_string(),
                        actual: "different type".to_string(),
                    });
                }
            }
            TabletValue::Int64(v) => {
                if let ColumnValue::Int64(vals) = col_values {
                    vals.push(Some(v));
                } else {
                    return Err(TsFileError::TypeMismatch {
                        expected: "Int64".to_string(),
                        actual: "different type".to_string(),
                    });
                }
            }
            TabletValue::Float(v) => {
                if let ColumnValue::Float(vals) = col_values {
                    vals.push(Some(v));
                } else {
                    return Err(TsFileError::TypeMismatch {
                        expected: "Float".to_string(),
                        actual: "different type".to_string(),
                    });
                }
            }
            TabletValue::Double(v) => {
                if let ColumnValue::Double(vals) = col_values {
                    vals.push(Some(v));
                } else {
                    return Err(TsFileError::TypeMismatch {
                        expected: "Double".to_string(),
                        actual: "different type".to_string(),
                    });
                }
            }
            TabletValue::String(v) => {
                if let ColumnValue::String(vals) = col_values {
                    vals.push(Some(v));
                } else {
                    return Err(TsFileError::TypeMismatch {
                        expected: "String".to_string(),
                        actual: "different type".to_string(),
                    });
                }
            }
            TabletValue::Null => {
                // Push null value
                match col_values {
                    ColumnValue::Boolean(vals) => vals.push(None),
                    ColumnValue::Int32(vals) => vals.push(None),
                    ColumnValue::Int64(vals) => vals.push(None),
                    ColumnValue::Float(vals) => vals.push(None),
                    ColumnValue::Double(vals) => vals.push(None),
                    ColumnValue::String(vals) => vals.push(None),
                }
            }
        }

        Ok(())
    }

    /// Reset the tablet for reuse
    pub fn reset(&mut self) {
        self.timestamps.clear();
        for value in self.values.values_mut() {
            match value {
                ColumnValue::Boolean(v) => v.clear(),
                ColumnValue::Int32(v) => v.clear(),
                ColumnValue::Int64(v) => v.clear(),
                ColumnValue::Float(v) => v.clear(),
                ColumnValue::Double(v) => v.clear(),
                ColumnValue::String(v) => v.clear(),
            }
        }
        self.row_count = 0;
    }

    /// Get the number of rows
    pub fn row_count(&self) -> usize {
        self.row_count
    }

    /// Get the number of columns
    pub fn column_count(&self) -> usize {
        self.column_names.len()
    }
}

/// Generic value type for adding to tablets
pub enum TabletValue {
    Boolean(bool),
    Int32(i32),
    Int64(i64),
    Float(f32),
    Double(f64),
    String(String),
    Null,
}

impl From<bool> for TabletValue {
    fn from(v: bool) -> Self {
        TabletValue::Boolean(v)
    }
}

impl From<i32> for TabletValue {
    fn from(v: i32) -> Self {
        TabletValue::Int32(v)
    }
}

impl From<i64> for TabletValue {
    fn from(v: i64) -> Self {
        TabletValue::Int64(v)
    }
}

impl From<f32> for TabletValue {
    fn from(v: f32) -> Self {
        TabletValue::Float(v)
    }
}

impl From<f64> for TabletValue {
    fn from(v: f64) -> Self {
        TabletValue::Double(v)
    }
}

impl From<String> for TabletValue {
    fn from(v: String) -> Self {
        TabletValue::String(v)
    }
}

impl From<&str> for TabletValue {
    fn from(v: &str) -> Self {
        TabletValue::String(v.to_string())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_tablet_creation() {
        let tablet = Tablet::new("test_table", vec!["col1", "col2"], 100);
        assert_eq!(tablet.table_name, "test_table");
        assert_eq!(tablet.column_count(), 2);
        assert_eq!(tablet.row_count(), 0);
    }

    #[test]
    fn test_tablet_with_schema() {
        let tablet = Tablet::with_schema(
            "test_table",
            vec!["id1", "s1"],
            vec![TSDataType::String, TSDataType::Int64],
            vec![ColumnCategory::Tag, ColumnCategory::Field],
            100,
        );
        assert_eq!(tablet.column_count(), 2);
    }

    #[test]
    fn test_tablet_reset() {
        let mut tablet = Tablet::new("test_table", vec!["col1"], 10);
        tablet.add_timestamp(0, 100);
        assert_eq!(tablet.timestamps.len(), 1);
        tablet.reset();
        assert_eq!(tablet.timestamps.len(), 0);
        assert_eq!(tablet.row_count(), 0);
    }
}
