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

//! Basic example showing TsFile encoding and compression capabilities

use tsfile::compress::{compress, decompress};
use tsfile::common::CompressionType;
use tsfile::encoding::{GorillaEncoder, PlainEncoder, RleEncoder, Ts2DiffEncoder};

fn main() {
    println!("=== Apache TsFile Rust Implementation Demo ===\n");

    // Demo 1: Plain encoding
    demo_plain_encoding();

    // Demo 2: RLE encoding
    demo_rle_encoding();

    // Demo 3: TS_2DIFF encoding
    demo_ts2diff_encoding();

    // Demo 4: Gorilla encoding
    demo_gorilla_encoding();

    // Demo 5: Compression
    demo_compression();
}

fn demo_plain_encoding() {
    println!("--- Plain Encoding Demo ---");

    let mut buf = Vec::new();

    // Encode various data types
    PlainEncoder::encode_bool(&mut buf, true).unwrap();
    PlainEncoder::encode_i32(&mut buf, 42).unwrap();
    PlainEncoder::encode_i64(&mut buf, 1234567890).unwrap();
    PlainEncoder::encode_f32(&mut buf, 3.14).unwrap();
    PlainEncoder::encode_f64(&mut buf, 2.718281828).unwrap();
    PlainEncoder::encode_string(&mut buf, "Hello, TsFile!").unwrap();

    println!("Encoded {} bytes of mixed data types", buf.len());
    println!();
}

fn demo_rle_encoding() {
    println!("--- RLE Encoding Demo ---");

    // Boolean array with many repetitions
    let bool_values = vec![true; 1000];
    let mut buf = Vec::new();
    RleEncoder::encode_bool_array(&mut buf, &bool_values).unwrap();

    let original_size = bool_values.len();
    let encoded_size = buf.len();
    let ratio = original_size as f64 / encoded_size as f64;

    println!("Boolean array: {} values", bool_values.len());
    println!("Original size: {} bytes", original_size);
    println!("Encoded size: {} bytes", encoded_size);
    println!("Compression ratio: {:.2}x", ratio);

    // Integer array with repetitions
    let int_values = vec![42; 500];
    let mut buf = Vec::new();
    RleEncoder::encode_i32_array(&mut buf, &int_values).unwrap();

    let original_size = int_values.len() * 4;
    let encoded_size = buf.len();
    let ratio = original_size as f64 / encoded_size as f64;

    println!("\nInteger array: {} values", int_values.len());
    println!("Original size: {} bytes", original_size);
    println!("Encoded size: {} bytes", encoded_size);
    println!("Compression ratio: {:.2}x", ratio);
    println!();
}

fn demo_ts2diff_encoding() {
    println!("--- TS_2DIFF Encoding Demo (for timestamps) ---");

    // Simulated timestamps with constant interval
    let start = 1609459200000i64; // 2021-01-01 00:00:00
    let interval = 1000i64; // 1 second
    let timestamps: Vec<i64> = (0..1000).map(|i| start + i * interval).collect();

    let mut buf = Vec::new();
    Ts2DiffEncoder::encode_i64_array(&mut buf, &timestamps).unwrap();

    let original_size = timestamps.len() * 8;
    let encoded_size = buf.len();
    let ratio = original_size as f64 / encoded_size as f64;

    println!("Timestamp array: {} values", timestamps.len());
    println!("Original size: {} bytes", original_size);
    println!("Encoded size: {} bytes", encoded_size);
    println!("Compression ratio: {:.2}x", ratio);
    println!("Note: Delta-of-deltas are all zeros (constant interval)");
    println!();
}

fn demo_gorilla_encoding() {
    println!("--- Gorilla Encoding Demo (for floats) ---");

    // Slowly changing temperature sensor data
    let temperatures: Vec<f32> = (0..1000).map(|i| 20.0 + (i as f32) * 0.01).collect();

    let mut buf = Vec::new();
    GorillaEncoder::encode_f32_array(&mut buf, &temperatures).unwrap();

    let original_size = temperatures.len() * 4;
    let encoded_size = buf.len();
    let ratio = original_size as f64 / encoded_size as f64;

    println!("Temperature readings: {} values", temperatures.len());
    println!("Original size: {} bytes", original_size);
    println!("Encoded size: {} bytes", encoded_size);
    println!("Compression ratio: {:.2}x", ratio);
    println!();
}

fn demo_compression() {
    println!("--- Compression Demo ---");

    let data = b"Hello, TsFile! This is a test of compression. ".repeat(100);
    let original_size = data.len();

    println!("Original data: {} bytes\n", original_size);

    // Test different compression methods
    for compression in &[
        CompressionType::Snappy,
        CompressionType::Gzip,
        CompressionType::LZ4,
    ] {
        if !compression.is_supported() {
            println!("{}: Not supported (feature not enabled)", compression.name());
            continue;
        }

        let compressed = compress(&data, *compression).unwrap();
        let compressed_size = compressed.len();
        let ratio = original_size as f64 / compressed_size as f64;

        // Verify decompression works
        let decompressed = decompress(&compressed, *compression, Some(original_size)).unwrap();
        assert_eq!(decompressed, data);

        println!(
            "{}: {} bytes ({}x compression)",
            compression.name(),
            compressed_size,
            ratio
        );
    }
    println!();
}
