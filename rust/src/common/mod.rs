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

//! Common data types and structures for TsFile

mod compression;
mod data_type;
mod encoding;
mod schema;
mod time_range;
pub mod tsfile_constants;

pub use compression::CompressionType;
pub use data_type::TSDataType;
pub use encoding::TSEncoding;
pub use schema::{ColumnCategory, ColumnSchema, MeasurementSchema, TableSchema};
pub use time_range::TimeRange;

/// Timestamp type (64-bit signed integer representing time in milliseconds)
pub type Timestamp = i64;

/// Ordering for sorting
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Ordering {
    /// Descending order
    Desc,
    /// Ascending order
    Asc,
}
