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

//! Benchmarks for TsFile library

use criterion::{black_box, criterion_group, criterion_main, Criterion, BenchmarkId};
use tempfile::NamedTempFile;
use tsfile::common::{ColumnCategory, ColumnSchema, CompressionType, TSDataType, TSEncoding, TableSchema};
use tsfile::reader::TsFileReader;
use tsfile::writer::{Tablet, TsFileWriter};

fn benchmark_write(c: &mut Criterion) {
    let mut group = c.benchmark_group("write");

    for size in [100, 1000, 10000].iter() {
        group.bench_with_input(BenchmarkId::from_parameter(size), size, |b, &size| {
            b.iter(|| {
                let temp_file = NamedTempFile::new().unwrap();
                let path = temp_file.path();

                let schema = TableSchema::new(
                    "bench_table",
                    vec![
                        ColumnSchema::field("temperature", TSDataType::Float),
                        ColumnSchema::field("humidity", TSDataType::Float),
                    ],
                );

                let mut writer = TsFileWriter::new(path, schema).unwrap();

                let mut tablet = Tablet::with_schema(
                    "bench_table",
                    vec!["temperature", "humidity"],
                    vec![TSDataType::Float, TSDataType::Float],
                    vec![ColumnCategory::Field, ColumnCategory::Field],
                    size,
                );

                for i in 0..size {
                    tablet.add_timestamp(i, i as i64);
                    tablet.add_value(i, "temperature", 20.0 + (i as f32 * 0.1)).unwrap();
                    tablet.add_value(i, "humidity", 50.0 + (i as f32 * 0.2)).unwrap();
                }

                writer.write_tablet(&tablet).unwrap();
                writer.close().unwrap();

                black_box(path);
            });
        });
    }

    group.finish();
}

fn benchmark_read(c: &mut Criterion) {
    let mut group = c.benchmark_group("read");

    for size in [100, 1000, 10000].iter() {
        // Prepare test file
        let temp_file = NamedTempFile::new().unwrap();
        let path = temp_file.path().to_path_buf();

        {
            let schema = TableSchema::new(
                "bench_table",
                vec![
                    ColumnSchema::field("temperature", TSDataType::Float),
                ],
            );

            let mut writer = TsFileWriter::new(&path, schema).unwrap();

            let mut tablet = Tablet::with_schema(
                "bench_table",
                vec!["temperature"],
                vec![TSDataType::Float],
                vec![ColumnCategory::Field],
                *size,
            );

            for i in 0..*size {
                tablet.add_timestamp(i, i as i64);
                tablet.add_value(i, "temperature", 20.0 + (i as f32 * 0.1)).unwrap();
            }

            writer.write_tablet(&tablet).unwrap();
            writer.close().unwrap();
        }

        group.bench_with_input(BenchmarkId::from_parameter(size), size, |b, _| {
            b.iter(|| {
                let mut reader = TsFileReader::open(&path).unwrap();
                let mut result_set = reader.read_all().unwrap();

                let mut count = 0;
                while result_set.next().unwrap() {
                    count += 1;
                }

                reader.close().unwrap();
                black_box(count);
            });
        });
    }

    group.finish();
}

fn benchmark_compression(c: &mut Criterion) {
    let mut group = c.benchmark_group("compression");

    let size = 1000;

    for compression in &[CompressionType::Uncompressed, CompressionType::Snappy] {
        group.bench_with_input(BenchmarkId::from_parameter(format!("{:?}", compression)), compression, |b, compression| {
            b.iter(|| {
                let temp_file = NamedTempFile::new().unwrap();
                let path = temp_file.path();

                let schema = TableSchema::new(
                    "bench_table",
                    vec![
                        ColumnSchema::new(
                            "temperature",
                            TSDataType::Float,
                            *compression,
                            TSEncoding::Plain,
                            ColumnCategory::Field,
                        ),
                    ],
                );

                let mut writer = TsFileWriter::new(path, schema).unwrap();

                let mut tablet = Tablet::with_schema(
                    "bench_table",
                    vec!["temperature"],
                    vec![TSDataType::Float],
                    vec![ColumnCategory::Field],
                    size,
                );

                for i in 0..size {
                    tablet.add_timestamp(i, i as i64);
                    tablet.add_value(i, "temperature", 20.0 + (i as f32 * 0.1)).unwrap();
                }

                writer.write_tablet(&tablet).unwrap();
                writer.close().unwrap();

                black_box(path);
            });
        });
    }

    group.finish();
}

fn benchmark_data_types(c: &mut Criterion) {
    let mut group = c.benchmark_group("data_types");

    let size = 1000;
    let data_types = vec![
        ("int32", TSDataType::Int32),
        ("int64", TSDataType::Int64),
        ("float", TSDataType::Float),
        ("double", TSDataType::Double),
        ("bool", TSDataType::Boolean),
    ];

    for (name, data_type) in data_types {
        group.bench_with_input(BenchmarkId::from_parameter(name), &data_type, |b, &data_type| {
            b.iter(|| {
                let temp_file = NamedTempFile::new().unwrap();
                let path = temp_file.path();

                let schema = TableSchema::new(
                    "bench_table",
                    vec![
                        ColumnSchema::field("value", data_type),
                    ],
                );

                let mut writer = TsFileWriter::new(path, schema).unwrap();

                let mut tablet = Tablet::with_schema(
                    "bench_table",
                    vec!["value"],
                    vec![data_type],
                    vec![ColumnCategory::Field],
                    size,
                );

                for i in 0..size {
                    tablet.add_timestamp(i, i as i64);
                    match data_type {
                        TSDataType::Int32 => tablet.add_value(i, "value", i as i32).unwrap(),
                        TSDataType::Int64 => tablet.add_value(i, "value", i as i64).unwrap(),
                        TSDataType::Float => tablet.add_value(i, "value", i as f32).unwrap(),
                        TSDataType::Double => tablet.add_value(i, "value", i as f64).unwrap(),
                        TSDataType::Boolean => tablet.add_value(i, "value", i % 2 == 0).unwrap(),
                        _ => {}
                    }
                }

                writer.write_tablet(&tablet).unwrap();
                writer.close().unwrap();

                black_box(path);
            });
        });
    }

    group.finish();
}

criterion_group!(benches, benchmark_write, benchmark_read, benchmark_compression, benchmark_data_types);
criterion_main!(benches);
