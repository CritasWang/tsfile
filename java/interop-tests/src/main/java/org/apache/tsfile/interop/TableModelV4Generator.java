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

package org.apache.tsfile.interop;

import org.apache.tsfile.enums.ColumnCategory;
import org.apache.tsfile.enums.TSDataType;
import org.apache.tsfile.file.metadata.ColumnSchemaBuilder;
import org.apache.tsfile.file.metadata.TableSchema;
import org.apache.tsfile.file.metadata.enums.CompressionType;
import org.apache.tsfile.write.record.Tablet;
import org.apache.tsfile.write.v4.ITsFileWriter;
import org.apache.tsfile.write.v4.TsFileWriterBuilder;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;

import java.io.File;
import java.io.FileWriter;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Generates comprehensive Table Model V4 test files covering various encoding and compression
 * combinations for C# interoperability testing.
 */
public class TableModelV4Generator {

  private static final int ROWS_PER_FILE = 100;
  private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();

  public static void main(String[] args) {
    try {
      String outputDir = args.length > 0 ? args[0] : "/tmp/table-model-v4-interop";
      File dir = new File(outputDir);
      if (!dir.exists()) {
        dir.mkdirs();
      }

      Map<String, Object> metadata = new HashMap<>();
      List<Map<String, Object>> testFiles = new ArrayList<>();

      // Generate test files for each data type
      testFiles.addAll(generateInt32Tests(outputDir));
      testFiles.addAll(generateInt64Tests(outputDir));
      testFiles.addAll(generateFloatTests(outputDir));
      testFiles.addAll(generateDoubleTests(outputDir));
      testFiles.addAll(generateBooleanTests(outputDir));
      testFiles.addAll(generateTextTests(outputDir));

      metadata.put("testFiles", testFiles);
      metadata.put("rowsPerFile", ROWS_PER_FILE);
      metadata.put("generatedAt", System.currentTimeMillis());

      // Write metadata JSON
      String metadataPath = outputDir + "/test-metadata.json";
      try (FileWriter writer = new FileWriter(metadataPath)) {
        GSON.toJson(metadata, writer);
      }

      System.out.println(
          "Successfully generated " + testFiles.size() + " test files in: " + outputDir);
      System.out.println("Metadata written to: " + metadataPath);
    } catch (Exception e) {
      System.err.println("Error generating test files: " + e.getMessage());
      e.printStackTrace();
      System.exit(1);
    }
  }

  private static List<Map<String, Object>> generateInt32Tests(String outputDir) throws Exception {
    List<Map<String, Object>> files = new ArrayList<>();
    String[] compressions = {"UNCOMPRESSED", "GZIP", "LZ4", "SNAPPY", "ZSTD"};
    String[] patterns = {"sequential", "repeated", "alternating"};

    for (String compression : compressions) {
      for (String pattern : patterns) {
        files.add(generateTableFile(outputDir, TSDataType.INT32, compression, pattern));
      }
    }
    return files;
  }

  private static List<Map<String, Object>> generateInt64Tests(String outputDir) throws Exception {
    List<Map<String, Object>> files = new ArrayList<>();
    String[] compressions = {"UNCOMPRESSED", "GZIP", "LZ4", "SNAPPY", "ZSTD"};
    String[] patterns = {"sequential", "repeated", "alternating"};

    for (String compression : compressions) {
      for (String pattern : patterns) {
        files.add(generateTableFile(outputDir, TSDataType.INT64, compression, pattern));
      }
    }
    return files;
  }

  private static List<Map<String, Object>> generateFloatTests(String outputDir) throws Exception {
    List<Map<String, Object>> files = new ArrayList<>();
    String[] compressions = {"UNCOMPRESSED", "GZIP", "LZ4", "SNAPPY", "ZSTD"};
    String[] patterns = {"sequential", "repeated", "alternating"};

    for (String compression : compressions) {
      for (String pattern : patterns) {
        files.add(generateTableFile(outputDir, TSDataType.FLOAT, compression, pattern));
      }
    }
    return files;
  }

  private static List<Map<String, Object>> generateDoubleTests(String outputDir) throws Exception {
    List<Map<String, Object>> files = new ArrayList<>();
    String[] compressions = {"UNCOMPRESSED", "GZIP", "LZ4", "SNAPPY", "ZSTD"};
    String[] patterns = {"sequential", "repeated", "alternating"};

    for (String compression : compressions) {
      for (String pattern : patterns) {
        files.add(generateTableFile(outputDir, TSDataType.DOUBLE, compression, pattern));
      }
    }
    return files;
  }

  private static List<Map<String, Object>> generateBooleanTests(String outputDir) throws Exception {
    List<Map<String, Object>> files = new ArrayList<>();
    String[] compressions = {"UNCOMPRESSED", "GZIP", "LZ4", "SNAPPY", "ZSTD"};
    String[] patterns = {"sequential", "repeated", "alternating"};

    for (String compression : compressions) {
      for (String pattern : patterns) {
        files.add(generateTableFile(outputDir, TSDataType.BOOLEAN, compression, pattern));
      }
    }
    return files;
  }

  private static List<Map<String, Object>> generateTextTests(String outputDir) throws Exception {
    List<Map<String, Object>> files = new ArrayList<>();
    String[] compressions = {"UNCOMPRESSED", "GZIP", "LZ4", "SNAPPY", "ZSTD"};
    String[] patterns = {"sequential", "repeated", "alternating"};

    for (String compression : compressions) {
      for (String pattern : patterns) {
        files.add(generateTableFile(outputDir, TSDataType.TEXT, compression, pattern));
      }
    }
    return files;
  }

  private static Map<String, Object> generateTableFile(
      String outputDir, TSDataType dataType, String compressionName, String pattern)
      throws Exception {

    String fileName =
        String.format(
            "table_%s_%s_%s.tsfile",
            dataType.name().toLowerCase(), compressionName.toLowerCase(), pattern);

    File file = new File(outputDir, fileName);
    if (file.exists()) {
      Files.delete(file.toPath());
    }

    CompressionType compression = parseCompression(compressionName);
    String tableName = "test_table";

    // Create table schema with TAG and FIELD columns
    TableSchema tableSchema =
        new TableSchema(
            tableName,
            Arrays.asList(
                new ColumnSchemaBuilder()
                    .name("device_id")
                    .dataType(TSDataType.STRING)
                    .category(ColumnCategory.TAG)
                    .build(),
                new ColumnSchemaBuilder()
                    .name("sensor_value")
                    .dataType(dataType)
                    .category(ColumnCategory.FIELD)
                    .build()));

    List<Object> expectedValues = new ArrayList<>();

    try (ITsFileWriter writer =
        new TsFileWriterBuilder()
            .file(file)
            .tableSchema(tableSchema)
            .memoryThreshold(1024 * 1024)
            .build()) {

      Tablet tablet =
          new Tablet(
              Arrays.asList("device_id", "sensor_value"),
              Arrays.asList(TSDataType.STRING, dataType));

      for (int row = 0; row < ROWS_PER_FILE; row++) {
        long timestamp = row * 1000L;
        tablet.addTimestamp(row, timestamp);
        tablet.addValue(row, "device_id", "device_001");

        Object value = generateValue(dataType, pattern, row);
        expectedValues.add(value);

        switch (dataType) {
          case INT32:
            tablet.addValue(row, "sensor_value", (int) value);
            break;
          case INT64:
            tablet.addValue(row, "sensor_value", (long) value);
            break;
          case FLOAT:
            tablet.addValue(row, "sensor_value", (float) value);
            break;
          case DOUBLE:
            tablet.addValue(row, "sensor_value", (double) value);
            break;
          case BOOLEAN:
            tablet.addValue(row, "sensor_value", (boolean) value);
            break;
          case TEXT:
            tablet.addValue(row, "sensor_value", (String) value);
            break;
        }
      }

      writer.write(tablet);
    }

    Map<String, Object> metadata = new HashMap<>();
    metadata.put("fileName", fileName);
    metadata.put("dataType", dataType.name());
    metadata.put("compression", compressionName);
    metadata.put("pattern", pattern);
    metadata.put("rowCount", ROWS_PER_FILE);
    metadata.put("expectedValues", expectedValues);

    System.out.println("Generated: " + fileName);
    return metadata;
  }

  private static CompressionType parseCompression(String name) {
    switch (name.toUpperCase()) {
      case "UNCOMPRESSED":
        return CompressionType.UNCOMPRESSED;
      case "GZIP":
        return CompressionType.GZIP;
      case "LZ4":
        return CompressionType.LZ4;
      case "SNAPPY":
        return CompressionType.SNAPPY;
      case "ZSTD":
        return CompressionType.ZSTD;
      default:
        throw new IllegalArgumentException("Unknown compression: " + name);
    }
  }

  private static Object generateValue(TSDataType dataType, String pattern, int index) {
    switch (pattern) {
      case "sequential":
        return generateSequentialValue(dataType, index);
      case "repeated":
        return generateRepeatedValue(dataType, index);
      case "alternating":
        return generateAlternatingValue(dataType, index);
      default:
        throw new IllegalArgumentException("Unknown pattern: " + pattern);
    }
  }

  private static Object generateSequentialValue(TSDataType dataType, int index) {
    switch (dataType) {
      case INT32:
        return index;
      case INT64:
        return (long) index;
      case FLOAT:
        return (float) index;
      case DOUBLE:
        return (double) index;
      case BOOLEAN:
        return index % 2 == 0;
      case TEXT:
        return "value_" + index;
      default:
        throw new IllegalArgumentException("Unsupported data type: " + dataType);
    }
  }

  private static Object generateRepeatedValue(TSDataType dataType, int index) {
    int groupSize = 10;
    int groupValue = index / groupSize;
    switch (dataType) {
      case INT32:
        return groupValue;
      case INT64:
        return (long) groupValue;
      case FLOAT:
        return (float) groupValue;
      case DOUBLE:
        return (double) groupValue;
      case BOOLEAN:
        return groupValue % 2 == 0;
      case TEXT:
        return "value_" + groupValue;
      default:
        throw new IllegalArgumentException("Unsupported data type: " + dataType);
    }
  }

  private static Object generateAlternatingValue(TSDataType dataType, int index) {
    switch (dataType) {
      case INT32:
        return index % 2 == 0 ? 100 : 200;
      case INT64:
        return index % 2 == 0 ? 100L : 200L;
      case FLOAT:
        return index % 2 == 0 ? 100.0f : 200.0f;
      case DOUBLE:
        return index % 2 == 0 ? 100.0 : 200.0;
      case BOOLEAN:
        return index % 2 == 0;
      case TEXT:
        return index % 2 == 0 ? "valueA" : "valueB";
      default:
        throw new IllegalArgumentException("Unsupported data type: " + dataType);
    }
  }
}
