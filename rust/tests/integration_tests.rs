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

//! Integration tests for TsFile library

use tempfile::NamedTempFile;
use tsfile::common::{
    ColumnCategory, ColumnSchema, CompressionType, TSDataType, TSEncoding, TableSchema, TimeRange,
};
use tsfile::reader::TsFileReader;
use tsfile::writer::{Tablet, TsFileWriter};

#[test]
fn test_write_read_all_data_types() {
    let temp_file = NamedTempFile::new().unwrap();
    let path = temp_file.path();

    // Write data with all supported data types
    {
        let schema = TableSchema::new(
            "all_types",
            vec![
                ColumnSchema::field("bool_col", TSDataType::Boolean),
                ColumnSchema::field("int32_col", TSDataType::Int32),
                ColumnSchema::field("int64_col", TSDataType::Int64),
                ColumnSchema::field("float_col", TSDataType::Float),
                ColumnSchema::field("double_col", TSDataType::Double),
                ColumnSchema::field("string_col", TSDataType::String),
            ],
        );

        let mut writer = TsFileWriter::new(path, schema).unwrap();

        let mut tablet = Tablet::with_schema(
            "all_types",
            vec![
                "bool_col",
                "int32_col",
                "int64_col",
                "float_col",
                "double_col",
                "string_col",
            ],
            vec![
                TSDataType::Boolean,
                TSDataType::Int32,
                TSDataType::Int64,
                TSDataType::Float,
                TSDataType::Double,
                TSDataType::String,
            ],
            vec![
                ColumnCategory::Field,
                ColumnCategory::Field,
                ColumnCategory::Field,
                ColumnCategory::Field,
                ColumnCategory::Field,
                ColumnCategory::Field,
            ],
            10,
        );

        for i in 0..3 {
            tablet.add_timestamp(i, (i * 1000) as i64);
            tablet.add_value(i, "bool_col", i % 2 == 0).unwrap();
            tablet.add_value(i, "int32_col", i as i32 * 10).unwrap();
            tablet.add_value(i, "int64_col", i as i64 * 100).unwrap();
            tablet.add_value(i, "float_col", i as f32 * 1.5).unwrap();
            tablet.add_value(i, "double_col", i as f64 * 2.5).unwrap();
            tablet
                .add_value(i, "string_col", format!("value_{}", i))
                .unwrap();
        }

        writer.write_tablet(&tablet).unwrap();
        writer.close().unwrap();
    }

    // Read and verify data
    {
        let mut reader = TsFileReader::open(path).unwrap();
        let result_set = reader.read_all().unwrap();

        // We wrote 3 rows * 6 columns = 18 total value entries
        assert_eq!(result_set.row_count(), 18);

        reader.close().unwrap();
    }
}

#[test]
fn test_write_read_with_compression() {
    let compressions = vec![
        CompressionType::Uncompressed,
        CompressionType::Snappy,
        #[cfg(feature = "gzip")]
        CompressionType::Gzip,
        // LZ4 requires original size, skip for now
        // #[cfg(feature = "lz4")]
        // CompressionType::LZ4,
    ];

    for compression in &compressions {
        let temp_file = NamedTempFile::new().unwrap();
        let path = temp_file.path();

        // Write data
        {
            let schema = TableSchema::new(
                "test_compression",
                vec![ColumnSchema::new(
                    "temperature",
                    TSDataType::Float,
                    *compression,
                    TSEncoding::Plain,
                    ColumnCategory::Field,
                )],
            );

            let mut writer = TsFileWriter::new(path, schema).unwrap();

            let mut tablet = Tablet::with_schema(
                "test_compression",
                vec!["temperature"],
                vec![TSDataType::Float],
                vec![ColumnCategory::Field],
                100,
            );

            for i in 0..50 {
                tablet.add_timestamp(i, i as i64);
                tablet
                    .add_value(i, "temperature", 20.0 + (i as f32 * 0.1))
                    .unwrap();
            }

            writer.write_tablet(&tablet).unwrap();
            writer.close().unwrap();
        }

        // Read and verify data
        {
            let mut reader = TsFileReader::open(path).unwrap();
            let result_set = reader.read_all().unwrap();

            assert_eq!(result_set.row_count(), 50);

            reader.close().unwrap();
        }
    }
}

#[test]
fn test_write_read_large_dataset() {
    let temp_file = NamedTempFile::new().unwrap();
    let path = temp_file.path();

    let num_rows = 1000;

    // Write large dataset
    {
        let schema = TableSchema::new(
            "large_data",
            vec![
                ColumnSchema::field("sensor1", TSDataType::Double),
                ColumnSchema::field("sensor2", TSDataType::Double),
                ColumnSchema::field("sensor3", TSDataType::Double),
            ],
        );

        let mut writer = TsFileWriter::new(path, schema).unwrap();

        let mut tablet = Tablet::with_schema(
            "large_data",
            vec!["sensor1", "sensor2", "sensor3"],
            vec![TSDataType::Double, TSDataType::Double, TSDataType::Double],
            vec![
                ColumnCategory::Field,
                ColumnCategory::Field,
                ColumnCategory::Field,
            ],
            num_rows,
        );

        for i in 0..num_rows {
            tablet.add_timestamp(i, i as i64);
            tablet.add_value(i, "sensor1", (i as f64).sin()).unwrap();
            tablet.add_value(i, "sensor2", (i as f64).cos()).unwrap();
            tablet.add_value(i, "sensor3", (i as f64).sqrt()).unwrap();
        }

        writer.write_tablet(&tablet).unwrap();
        writer.close().unwrap();
    }

    // Read and verify
    {
        let mut reader = TsFileReader::open(path).unwrap();
        let result_set = reader.read_all().unwrap();

        // 1000 rows * 3 columns = 3000 total entries
        assert_eq!(result_set.row_count(), num_rows * 3);

        reader.close().unwrap();
    }
}

#[test]
fn test_time_range_query() {
    let temp_file = NamedTempFile::new().unwrap();
    let path = temp_file.path();

    // Write data with timestamps 0-999
    {
        let schema = TableSchema::new(
            "time_series",
            vec![ColumnSchema::field("value", TSDataType::Int64)],
        );

        let mut writer = TsFileWriter::new(path, schema).unwrap();

        let mut tablet = Tablet::with_schema(
            "time_series",
            vec!["value"],
            vec![TSDataType::Int64],
            vec![ColumnCategory::Field],
            1000,
        );

        for i in 0..1000 {
            tablet.add_timestamp(i, i as i64);
            tablet.add_value(i, "value", i as i64 * 10).unwrap();
        }

        writer.write_tablet(&tablet).unwrap();
        writer.close().unwrap();
    }

    // Test various time range queries
    {
        let mut reader = TsFileReader::open(path).unwrap();

        // Query range [100, 200]
        let time_range = TimeRange::new(100, 200);
        let mut result_set = reader.read_with_filter(Some(time_range)).unwrap();
        assert_eq!(result_set.row_count(), 101); // 100-200 inclusive = 101 values

        // Query range [500, 599]
        let time_range = TimeRange::new(500, 599);
        result_set = reader.read_with_filter(Some(time_range)).unwrap();
        assert_eq!(result_set.row_count(), 100); // 500-599 inclusive = 100 values

        // Query all data (no filter)
        result_set = reader.read_all().unwrap();
        assert_eq!(result_set.row_count(), 1000);

        reader.close().unwrap();
    }
}

#[test]
fn test_empty_tablet() {
    let temp_file = NamedTempFile::new().unwrap();
    let path = temp_file.path();

    // Write empty tablet (should be no-op)
    {
        let schema = TableSchema::new(
            "empty",
            vec![ColumnSchema::field("value", TSDataType::Int32)],
        );

        let mut writer = TsFileWriter::new(path, schema).unwrap();

        let tablet = Tablet::with_schema(
            "empty",
            vec!["value"],
            vec![TSDataType::Int32],
            vec![ColumnCategory::Field],
            10,
        );
        // Don't add any data

        writer.write_tablet(&tablet).unwrap();
        writer.close().unwrap();
    }

    // Read - should get empty result
    {
        let mut reader = TsFileReader::open(path).unwrap();
        let result_set = reader.read_all().unwrap();

        assert_eq!(result_set.row_count(), 0);

        reader.close().unwrap();
    }
}

#[test]
fn test_multiple_tablets() {
    let temp_file = NamedTempFile::new().unwrap();
    let path = temp_file.path();

    // Write multiple tablets
    {
        let schema = TableSchema::new(
            "multi_tablet",
            vec![ColumnSchema::field("value", TSDataType::Int32)],
        );

        let mut writer = TsFileWriter::new(path, schema).unwrap();

        // Write 3 tablets with different data
        for batch in 0..3 {
            let mut tablet = Tablet::with_schema(
                "multi_tablet",
                vec!["value"],
                vec![TSDataType::Int32],
                vec![ColumnCategory::Field],
                10,
            );

            for i in 0..10 {
                let timestamp = (batch * 100 + i) as i64;
                tablet.add_timestamp(i, timestamp);
                tablet
                    .add_value(i, "value", (batch * 100 + i) as i32)
                    .unwrap();
            }

            writer.write_tablet(&tablet).unwrap();
        }

        writer.close().unwrap();
    }

    // Read all
    {
        let mut reader = TsFileReader::open(path).unwrap();
        let result_set = reader.read_all().unwrap();

        // 3 tablets * 10 rows = 30 total
        assert_eq!(result_set.row_count(), 30);

        reader.close().unwrap();
    }
}
