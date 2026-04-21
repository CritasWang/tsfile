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

//! Example demonstrating TsFile write operations

use tsfile::common::{
    ColumnCategory, ColumnSchema, CompressionType, TSDataType, TSEncoding, TableSchema,
};
use tsfile::writer::{Tablet, TsFileWriter};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    println!("=== TsFile Writer Demo ===\n");

    // Create schema for a sensor data table
    let schema = TableSchema::new(
        "sensor_table",
        vec![
            ColumnSchema::new(
                "device_id",
                TSDataType::String,
                CompressionType::Snappy,
                TSEncoding::Plain,
                ColumnCategory::Tag,
            ),
            ColumnSchema::new(
                "temperature",
                TSDataType::Float,
                CompressionType::Snappy,
                TSEncoding::Plain,
                ColumnCategory::Field,
            ),
            ColumnSchema::new(
                "humidity",
                TSDataType::Float,
                CompressionType::Snappy,
                TSEncoding::Plain,
                ColumnCategory::Field,
            ),
            ColumnSchema::new(
                "pressure",
                TSDataType::Int64,
                CompressionType::Snappy,
                TSEncoding::Plain,
                ColumnCategory::Field,
            ),
        ],
    );

    println!("Creating TsFile writer with schema:");
    println!("  Table: {}", schema.table_name);
    println!("  Columns: {}", schema.column_count());
    for col in &schema.columns {
        println!(
            "    - {} ({}, {})",
            col.column_name, col.data_type, col.category
        );
    }
    println!();

    // Create writer
    let mut writer = TsFileWriter::new("demo_output.tsfile", schema.clone())?;
    println!("✓ TsFile writer created: demo_output.tsfile\n");

    // Create tablet with schema
    let mut tablet = Tablet::with_schema(
        "sensor_table",
        vec!["device_id", "temperature", "humidity", "pressure"],
        vec![
            TSDataType::String,
            TSDataType::Float,
            TSDataType::Float,
            TSDataType::Int64,
        ],
        vec![
            ColumnCategory::Tag,
            ColumnCategory::Field,
            ColumnCategory::Field,
            ColumnCategory::Field,
        ],
        100,
    );

    println!("Writing sensor data:");

    // Write 10 rows of sensor data
    let start_time = 1609459200000i64; // 2021-01-01 00:00:00
    for i in 0..10 {
        let timestamp = start_time + i * 1000; // 1 second intervals

        tablet.add_timestamp(i as usize, timestamp);
        tablet.add_value(i as usize, "device_id", "sensor_001")?;
        tablet.add_value(i as usize, "temperature", 20.0 + (i as f32) * 0.5)?;
        tablet.add_value(i as usize, "humidity", 50.0 + (i as f32) * 0.2)?;
        tablet.add_value(i as usize, "pressure", 1013 + i)?;

        println!(
            "  Row {}: timestamp={}, temp={:.1}°C, humidity={:.1}%, pressure={} hPa",
            i,
            timestamp,
            20.0 + (i as f32) * 0.5,
            50.0 + (i as f32) * 0.2,
            1013 + i
        );
    }
    println!();

    // Write the tablet
    println!("Writing tablet to TsFile...");
    writer.write_tablet(&tablet)?;
    println!("✓ Tablet written ({} rows)\n", tablet.row_count());

    // Flush and close
    println!("Flushing data...");
    writer.flush()?;
    println!("✓ Data flushed\n");

    println!("Closing writer...");
    writer.close()?;
    println!("✓ Writer closed\n");

    // Check the file
    use std::fs;
    let metadata = fs::metadata("demo_output.tsfile")?;
    println!("=== File Statistics ===");
    println!("  File: demo_output.tsfile");
    println!("  Size: {} bytes", metadata.len());
    println!();

    // Verify file format
    let contents = fs::read("demo_output.tsfile")?;
    println!("=== File Format Verification ===");
    println!(
        "  Magic header: {}",
        if &contents[0..6] == b"TsFile" {
            "✓ Valid"
        } else {
            "✗ Invalid"
        }
    );
    println!("  Version: 0x{:02x}", contents[6]);

    let footer_start = contents.len() - 10;
    println!(
        "  Magic footer: {}",
        if &contents[footer_start..footer_start + 6] == b"TsFile" {
            "✓ Valid"
        } else {
            "✗ Invalid"
        }
    );

    // Read metadata size from footer
    let metadata_size = i32::from_le_bytes([
        contents[contents.len() - 4],
        contents[contents.len() - 3],
        contents[contents.len() - 2],
        contents[contents.len() - 1],
    ]);
    println!("  Metadata size: {} bytes", metadata_size);
    println!();

    println!("=== Success! ===");
    println!("TsFile successfully created with valid format");
    println!("Data written: 10 rows × 4 columns = 40 data points");

    Ok(())
}
