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

//! TsFile metadata structures for proper format compatibility

use crate::common::{ColumnSchema, TableSchema};
use crate::error::Result;
use crate::utils::{write_var_int, write_var_string, write_var_uint};
use byteorder::{LittleEndian, WriteBytesExt};
use std::collections::HashMap;
use std::io::Write;

/// Type of metadata index node in the index tree
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
#[repr(u8)]
pub enum MetadataIndexNodeType {
    /// Internal nodes of the index tree's device level
    InternalDevice = 0,
    /// Leaf nodes of the index tree's device level, points to measurement level
    LeafDevice = 1,
    /// Internal nodes of the index tree's measurement level
    InternalMeasurement = 2,
    /// Leaf nodes of the index tree's measurement level, points to TimeseriesMetadata
    LeafMeasurement = 3,
}

impl MetadataIndexNodeType {
    /// Serialize to byte
    pub fn serialize(&self) -> u8 {
        *self as u8
    }

    /// Deserialize from byte
    pub fn deserialize(byte: u8) -> Option<Self> {
        match byte {
            0 => Some(MetadataIndexNodeType::InternalDevice),
            1 => Some(MetadataIndexNodeType::LeafDevice),
            2 => Some(MetadataIndexNodeType::InternalMeasurement),
            3 => Some(MetadataIndexNodeType::LeafMeasurement),
            _ => None,
        }
    }
}

/// Entry in a metadata index node
#[derive(Debug, Clone)]
pub struct MetadataIndexEntry {
    /// Name (device or measurement name)
    pub name: String,
    /// Offset in file
    pub offset: i64,
}

impl MetadataIndexEntry {
    /// Create a new metadata index entry
    pub fn new(name: String, offset: i64) -> Self {
        Self { name, offset }
    }

    /// Serialize to output stream
    pub fn serialize_to<W: Write>(&self, writer: &mut W) -> Result<usize> {
        let mut size = 0;
        size += write_var_string(writer, &self.name)?;
        writer.write_i64::<LittleEndian>(self.offset)?;
        size += 8; // i64
        Ok(size)
    }
}

/// Metadata index node representing a node in the metadata index tree
#[derive(Debug, Clone)]
pub struct MetadataIndexNode {
    /// Child entries
    pub children: Vec<MetadataIndexEntry>,
    /// End offset of this node
    pub end_offset: i64,
    /// Type of this node
    pub node_type: MetadataIndexNodeType,
}

impl MetadataIndexNode {
    /// Create a new metadata index node
    pub fn new(node_type: MetadataIndexNodeType) -> Self {
        Self {
            children: Vec::new(),
            end_offset: -1,
            node_type,
        }
    }

    /// Create a leaf device node (empty for minimal implementation)
    pub fn new_leaf_device() -> Self {
        Self::new(MetadataIndexNodeType::LeafDevice)
    }

    /// Serialize to output stream
    pub fn serialize_to<W: Write>(&self, writer: &mut W) -> Result<usize> {
        let mut size = 0;

        // Write number of children
        size += write_var_uint(writer, self.children.len() as u64)?;

        // Write each child entry
        for child in &self.children {
            size += child.serialize_to(writer)?;
        }

        // Write end offset
        writer.write_i64::<LittleEndian>(self.end_offset)?;
        size += 8;

        // Write node type
        writer.write_u8(self.node_type.serialize())?;
        size += 1;

        Ok(size)
    }
}

/// TsFile metadata structure
#[derive(Debug, Clone)]
pub struct TsFileMetadata {
    /// Table name to metadata index node map
    pub table_metadata_index_map: HashMap<String, MetadataIndexNode>,
    /// Table name to schema map
    pub table_schema_map: HashMap<String, TableSchema>,
    /// Offset of the separator marker
    pub meta_offset: i64,
    /// Bloom filter (optional, not implemented yet)
    pub bloom_filter: Option<Vec<u8>>,
    /// Properties map (for encryption, version info, etc.)
    pub properties: HashMap<String, String>,
}

impl TsFileMetadata {
    /// Create a new TsFile metadata
    pub fn new() -> Self {
        Self {
            table_metadata_index_map: HashMap::new(),
            table_schema_map: HashMap::new(),
            meta_offset: 0,
            bloom_filter: None,
            properties: HashMap::new(),
        }
    }

    /// Create minimal metadata for a table (for compatibility)
    pub fn with_table(table_name: String, schema: TableSchema, meta_offset: i64) -> Self {
        let mut metadata = Self::new();
        metadata.meta_offset = meta_offset;

        // Create empty metadata index node for this table
        let index_node = MetadataIndexNode::new_leaf_device();
        metadata
            .table_metadata_index_map
            .insert(table_name.clone(), index_node);
        metadata.table_schema_map.insert(table_name, schema);

        // Add default properties (unencrypted)
        metadata
            .properties
            .insert("encryptLevel".to_string(), "0".to_string());
        metadata.properties.insert(
            "encryptType".to_string(),
            "org.apache.tsfile.encrypt.UNENCRYPTED".to_string(),
        );
        metadata
            .properties
            .insert("encryptKey".to_string(), "".to_string());

        metadata
    }

    /// Serialize to output stream (compatible with Java/C++ format)
    pub fn serialize_to<W: Write>(&self, writer: &mut W) -> Result<usize> {
        let mut size = 0;

        // 1. Write table metadata index nodes
        size += write_var_uint(writer, self.table_metadata_index_map.len() as u64)?;
        for (table_name, index_node) in &self.table_metadata_index_map {
            size += write_var_string(writer, table_name)?;
            size += index_node.serialize_to(writer)?;
        }

        // 2. Write table schemas
        size += write_var_uint(writer, self.table_schema_map.len() as u64)?;
        for (table_name, schema) in &self.table_schema_map {
            size += write_var_string(writer, table_name)?;
            size += serialize_table_schema(writer, schema)?;
        }

        // 3. Write meta offset (i64)
        writer.write_i64::<LittleEndian>(self.meta_offset)?;
        size += 8;

        // 4. Write bloom filter (empty for now)
        if let Some(ref bloom_data) = self.bloom_filter {
            size += write_var_uint(writer, bloom_data.len() as u64)?;
            writer.write_all(bloom_data)?;
            size += bloom_data.len();
        } else {
            size += write_var_uint(writer, 0)?;
        }

        // 5. Write properties
        size += write_var_int(writer, self.properties.len() as i64)?;
        for (key, value) in &self.properties {
            size += write_var_string(writer, key)?;
            size += write_var_string(writer, value)?;
        }

        Ok(size)
    }
}

impl Default for TsFileMetadata {
    fn default() -> Self {
        Self::new()
    }
}

/// Serialize a TableSchema (compatible with Java format)
fn serialize_table_schema<W: Write>(writer: &mut W, schema: &TableSchema) -> Result<usize> {
    let mut size = 0;

    // Write number of columns
    size += write_var_uint(writer, schema.columns.len() as u64)?;

    // Write each column schema
    for column in &schema.columns {
        size += serialize_column_schema(writer, column)?;
    }

    Ok(size)
}

/// Serialize a ColumnSchema (compatible with Java MeasurementSchema format)
fn serialize_column_schema<W: Write>(writer: &mut W, column: &ColumnSchema) -> Result<usize> {
    let mut size = 0;

    // Write column name (as measurement name)
    size += write_var_string(writer, &column.column_name)?;

    // Write data type (byte)
    writer.write_u8(column.data_type.to_byte())?;
    size += 1;

    // Write encoding (byte)
    writer.write_u8(column.encoding.to_byte())?;
    size += 1;

    // Write compression type (byte)
    writer.write_u8(column.compression.to_byte())?;
    size += 1;

    // Write props map (empty map for now)
    size += write_var_int(writer, 0)?; // 0 properties

    // Write column category (as i32 for compatibility with Java)
    writer.write_i32::<LittleEndian>(column.category.to_byte() as i32)?;
    size += 4;

    Ok(size)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::common::TSDataType;

    #[test]
    fn test_metadata_index_node_type() {
        assert_eq!(MetadataIndexNodeType::InternalDevice.serialize(), 0);
        assert_eq!(MetadataIndexNodeType::LeafDevice.serialize(), 1);
        assert_eq!(MetadataIndexNodeType::InternalMeasurement.serialize(), 2);
        assert_eq!(MetadataIndexNodeType::LeafMeasurement.serialize(), 3);
    }

    #[test]
    fn test_metadata_serialization() {
        let schema = TableSchema::new(
            "test_table",
            vec![ColumnSchema::field("temperature", TSDataType::Float)],
        );

        let metadata = TsFileMetadata::with_table("test_table".to_string(), schema, 100);

        let mut buffer = Vec::new();
        let size = metadata.serialize_to(&mut buffer).unwrap();

        assert!(size > 0);
        assert!(!buffer.is_empty());
    }
}
