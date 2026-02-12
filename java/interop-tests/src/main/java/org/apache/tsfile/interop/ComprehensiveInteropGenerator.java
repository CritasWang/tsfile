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
import org.apache.tsfile.write.record.Tablet;
import org.apache.tsfile.write.schema.IMeasurementSchema;
import org.apache.tsfile.write.schema.MeasurementSchema;
import org.apache.tsfile.write.v4.ITsFileWriter;
import org.apache.tsfile.write.v4.TsFileTreeWriter;
import org.apache.tsfile.write.v4.TsFileTreeWriterBuilder;
import org.apache.tsfile.write.v4.TsFileWriterBuilder;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;

import java.io.File;
import java.io.FileWriter;
import java.nio.file.Files;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Generates comprehensive interop test files: 1) Table model: 3 tables, each with 3 TAG columns and
 * 10 data type FIELD columns 2) Tree model: multiple devices, both aligned and non-aligned
 * timeseries
 */
public class ComprehensiveInteropGenerator {

  private static final int ROWS_PER_DEVICE = 20;
  private static final Gson GSON = new GsonBuilder().setPrettyPrinting().create();

  public static void main(String[] args) {
    try {
      String outputDir = args.length > 0 ? args[0] : "/tmp/comprehensive-interop";
      File dir = new File(outputDir);
      if (!dir.exists()) {
        dir.mkdirs();
      }

      Map<String, Object> allMetadata = new LinkedHashMap<>();

      // 1) Generate table model file
      Map<String, Object> tableMeta = generateTableModelFile(outputDir);
      allMetadata.put("tableModel", tableMeta);

      // 2) Generate tree model file
      Map<String, Object> treeMeta = generateTreeModelFile(outputDir);
      allMetadata.put("treeModel", treeMeta);

      // Write combined metadata
      String metadataPath = outputDir + "/comprehensive-metadata.json";
      try (FileWriter writer = new FileWriter(metadataPath)) {
        GSON.toJson(allMetadata, writer);
      }

      System.out.println("All files generated in: " + outputDir);
      System.out.println("Metadata: " + metadataPath);
    } catch (Exception e) {
      System.err.println("Error: " + e.getMessage());
      e.printStackTrace();
      System.exit(1);
    }
  }

  // =========================================================================
  // TABLE MODEL: 3 tables × 3 TAG cols × 10 FIELD data types
  // =========================================================================
  private static Map<String, Object> generateTableModelFile(String outputDir) throws Exception {
    String path = outputDir + "/comprehensive_table_model.tsfile";
    File f = new File(path);
    if (f.exists()) Files.delete(f.toPath());

    // We generate 3 separate files (one per table) since DeviceTableModelWriter
    // only supports a single table per writer.
    List<Map<String, Object>> tables = new ArrayList<>();

    for (int t = 0; t < 3; t++) {
      String tablePath = outputDir + "/comprehensive_table_" + t + ".tsfile";
      File tf = new File(tablePath);
      if (tf.exists()) Files.delete(tf.toPath());

      String tableName = "table_" + t;
      Map<String, Object> tableMeta = generateSingleTable(tablePath, tableName, t);
      tables.add(tableMeta);
    }

    Map<String, Object> meta = new LinkedHashMap<>();
    meta.put("type", "tableModel");
    meta.put("tableCount", 3);
    meta.put("tables", tables);
    System.out.println("Generated table model files");
    return meta;
  }

  private static Map<String, Object> generateSingleTable(
      String path, String tableName, int tableIndex) throws Exception {

    File f = new File(path);

    // 3 TAG columns (all STRING) + 10 FIELD columns (one per data type)
    // Data types: BOOLEAN, INT32, INT64, FLOAT, DOUBLE, TEXT, STRING, TIMESTAMP, DATE, BLOB
    // Note: BLOB is not well-supported in tablet API, so we use TEXT instead for the 10th
    List<org.apache.tsfile.file.metadata.ColumnSchema> columns = new ArrayList<>();

    // TAG columns
    columns.add(
        new ColumnSchemaBuilder()
            .name("region")
            .dataType(TSDataType.STRING)
            .category(ColumnCategory.TAG)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("plant")
            .dataType(TSDataType.STRING)
            .category(ColumnCategory.TAG)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("device")
            .dataType(TSDataType.STRING)
            .category(ColumnCategory.TAG)
            .build());

    // FIELD columns — 10 different data types
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_boolean")
            .dataType(TSDataType.BOOLEAN)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_int32")
            .dataType(TSDataType.INT32)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_int64")
            .dataType(TSDataType.INT64)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_float")
            .dataType(TSDataType.FLOAT)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_double")
            .dataType(TSDataType.DOUBLE)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_text")
            .dataType(TSDataType.TEXT)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_string")
            .dataType(TSDataType.STRING)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_timestamp")
            .dataType(TSDataType.TIMESTAMP)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_date")
            .dataType(TSDataType.DATE)
            .category(ColumnCategory.FIELD)
            .build());
    columns.add(
        new ColumnSchemaBuilder()
            .name("f_blob")
            .dataType(TSDataType.BLOB)
            .category(ColumnCategory.FIELD)
            .build());

    TableSchema tableSchema = new TableSchema(tableName, columns);

    // Column names and types for Tablet
    List<String> colNames = new ArrayList<>();
    List<TSDataType> colTypes = new ArrayList<>();
    for (org.apache.tsfile.file.metadata.ColumnSchema col : columns) {
      colNames.add(col.getColumnName());
      colTypes.add(col.getDataType());
    }

    // Device combinations: 2 regions × 2 plants × 2 devices = 8 devices per table
    String[] regions = {"north", "south"};
    String[] plants = {"p1", "p2"};
    String[] devices = {"d1", "d2"};

    List<Map<String, Object>> deviceDataList = new ArrayList<>();

    try (ITsFileWriter writer =
        new TsFileWriterBuilder()
            .file(f)
            .tableSchema(tableSchema)
            .memoryThreshold(4 * 1024 * 1024)
            .build()) {

      Tablet tablet = new Tablet(colNames, colTypes);

      int rowIndex = 0;
      for (String region : regions) {
        for (String plant : plants) {
          for (String device : devices) {
            Map<String, Object> deviceData = new LinkedHashMap<>();
            deviceData.put("region", region);
            deviceData.put("plant", plant);
            deviceData.put("device", device);

            List<Map<String, Object>> rows = new ArrayList<>();

            for (int i = 0; i < ROWS_PER_DEVICE; i++) {
              long timestamp = (long) tableIndex * 1000000L + rowIndex * 1000L;
              int seed = tableIndex * 1000 + rowIndex;

              tablet.addTimestamp(rowIndex, timestamp);
              tablet.addValue(rowIndex, "region", region);
              tablet.addValue(rowIndex, "plant", plant);
              tablet.addValue(rowIndex, "device", device);

              boolean boolVal = seed % 2 == 0;
              int int32Val = seed * 10;
              long int64Val = seed * 100L;
              float floatVal = seed * 1.5f;
              double doubleVal = seed * 2.5;
              String textVal = "text_" + seed;
              String stringVal = "str_" + seed;
              long timestampVal = 1700000000000L + seed * 1000L;
              LocalDate dateVal = LocalDate.ofEpochDay(19000 + seed);
              byte[] blobVal = ("blob_" + seed).getBytes();

              tablet.addValue(rowIndex, "f_boolean", boolVal);
              tablet.addValue(rowIndex, "f_int32", int32Val);
              tablet.addValue(rowIndex, "f_int64", int64Val);
              tablet.addValue(rowIndex, "f_float", floatVal);
              tablet.addValue(rowIndex, "f_double", doubleVal);
              tablet.addValue(rowIndex, "f_text", textVal);
              tablet.addValue(rowIndex, "f_string", stringVal);
              tablet.addValue(rowIndex, "f_timestamp", timestampVal);
              tablet.addValue(rowIndex, "f_date", dateVal);
              tablet.addValue(rowIndex, "f_blob", blobVal);

              Map<String, Object> row = new LinkedHashMap<>();
              row.put("timestamp", timestamp);
              row.put("f_boolean", boolVal);
              row.put("f_int32", int32Val);
              row.put("f_int64", int64Val);
              row.put("f_float", floatVal);
              row.put("f_double", doubleVal);
              row.put("f_text", textVal);
              row.put("f_string", stringVal);
              row.put("f_timestamp", timestampVal);
              row.put("f_date", dateVal.toEpochDay());
              row.put("f_blob", "blob_" + seed);
              rows.add(row);

              rowIndex++;
            }

            deviceData.put("rowCount", ROWS_PER_DEVICE);
            deviceData.put("rows", rows);
            deviceDataList.add(deviceData);
          }
        }
      }

      writer.write(tablet);
    }

    Map<String, Object> meta = new LinkedHashMap<>();
    meta.put("fileName", f.getName());
    meta.put("tableName", tableName);
    meta.put("tagColumns", Arrays.asList("region", "plant", "device"));
    meta.put(
        "fieldColumns",
        Arrays.asList(
            "f_boolean",
            "f_int32",
            "f_int64",
            "f_float",
            "f_double",
            "f_text",
            "f_string",
            "f_timestamp",
            "f_date",
            "f_blob"));
    meta.put("deviceCount", deviceDataList.size());
    meta.put("totalRows", ROWS_PER_DEVICE * deviceDataList.size());
    meta.put("devices", deviceDataList);

    System.out.println(
        "  Generated: "
            + f.getName()
            + " ("
            + tableName
            + ", "
            + deviceDataList.size()
            + " devices)");
    return meta;
  }

  // =========================================================================
  // TREE MODEL: multiple devices, aligned + non-aligned
  // =========================================================================
  private static Map<String, Object> generateTreeModelFile(String outputDir) throws Exception {
    String path = outputDir + "/comprehensive_tree_model.tsfile";
    File f = new File(path);
    if (f.exists()) Files.delete(f.toPath());

    List<Map<String, Object>> deviceDataList = new ArrayList<>();

    try (TsFileTreeWriter writer =
        new TsFileTreeWriterBuilder().file(f).memoryThreshold(4 * 1024 * 1024).build()) {

      // === Non-aligned devices ===
      // Device 1: root.db1.d1 — INT32, INT64, FLOAT, DOUBLE, BOOLEAN
      {
        String deviceId = "root.db1.d1";
        IMeasurementSchema s1 = new MeasurementSchema("s_int32", TSDataType.INT32);
        IMeasurementSchema s2 = new MeasurementSchema("s_int64", TSDataType.INT64);
        IMeasurementSchema s3 = new MeasurementSchema("s_float", TSDataType.FLOAT);
        IMeasurementSchema s4 = new MeasurementSchema("s_double", TSDataType.DOUBLE);
        IMeasurementSchema s5 = new MeasurementSchema("s_boolean", TSDataType.BOOLEAN);

        writer.registerTimeseries(deviceId, s1);
        writer.registerTimeseries(deviceId, s2);
        writer.registerTimeseries(deviceId, s3);
        writer.registerTimeseries(deviceId, s4);
        writer.registerTimeseries(deviceId, s5);

        Tablet tablet = new Tablet(deviceId, Arrays.asList(s1, s2, s3, s4, s5), ROWS_PER_DEVICE);
        List<Map<String, Object>> rows = new ArrayList<>();

        for (int i = 0; i < ROWS_PER_DEVICE; i++) {
          long ts = i * 1000L;
          tablet.addTimestamp(i, ts);
          int v1 = i * 10;
          long v2 = i * 100L;
          float v3 = i * 1.5f;
          double v4 = i * 2.5;
          boolean v5 = i % 2 == 0;
          tablet.addValue(i, 0, v1);
          tablet.addValue(i, 1, v2);
          tablet.addValue(i, 2, v3);
          tablet.addValue(i, 3, v4);
          tablet.addValue(i, 4, v5);

          Map<String, Object> row = new LinkedHashMap<>();
          row.put("timestamp", ts);
          row.put("s_int32", v1);
          row.put("s_int64", v2);
          row.put("s_float", v3);
          row.put("s_double", v4);
          row.put("s_boolean", v5);
          rows.add(row);
        }
        writer.write(tablet);

        Map<String, Object> dd = new LinkedHashMap<>();
        dd.put("deviceId", deviceId);
        dd.put("aligned", false);
        dd.put(
            "measurements",
            Arrays.asList("s_int32", "s_int64", "s_float", "s_double", "s_boolean"));
        dd.put("dataTypes", Arrays.asList("INT32", "INT64", "FLOAT", "DOUBLE", "BOOLEAN"));
        dd.put("rowCount", ROWS_PER_DEVICE);
        dd.put("rows", rows);
        deviceDataList.add(dd);
      }

      // Device 2: root.db1.d2 — TEXT, STRING
      {
        String deviceId = "root.db1.d2";
        IMeasurementSchema s1 = new MeasurementSchema("s_text", TSDataType.TEXT);
        IMeasurementSchema s2 = new MeasurementSchema("s_string", TSDataType.STRING);

        writer.registerTimeseries(deviceId, s1);
        writer.registerTimeseries(deviceId, s2);

        Tablet tablet = new Tablet(deviceId, Arrays.asList(s1, s2), ROWS_PER_DEVICE);
        List<Map<String, Object>> rows = new ArrayList<>();

        for (int i = 0; i < ROWS_PER_DEVICE; i++) {
          long ts = i * 1000L;
          tablet.addTimestamp(i, ts);
          String v1 = "text_" + i;
          String v2 = "string_" + i;
          tablet.addValue(i, 0, v1);
          tablet.addValue(i, 1, v2);

          Map<String, Object> row = new LinkedHashMap<>();
          row.put("timestamp", ts);
          row.put("s_text", v1);
          row.put("s_string", v2);
          rows.add(row);
        }
        writer.write(tablet);

        Map<String, Object> dd = new LinkedHashMap<>();
        dd.put("deviceId", deviceId);
        dd.put("aligned", false);
        dd.put("measurements", Arrays.asList("s_text", "s_string"));
        dd.put("dataTypes", Arrays.asList("TEXT", "STRING"));
        dd.put("rowCount", ROWS_PER_DEVICE);
        dd.put("rows", rows);
        deviceDataList.add(dd);
      }

      // Device 3: root.db2.d1 — non-aligned, INT32 only (different database)
      {
        String deviceId = "root.db2.d1";
        IMeasurementSchema s1 = new MeasurementSchema("temperature", TSDataType.INT32);

        writer.registerTimeseries(deviceId, s1);

        Tablet tablet = new Tablet(deviceId, Arrays.asList(s1), ROWS_PER_DEVICE);
        List<Map<String, Object>> rows = new ArrayList<>();

        for (int i = 0; i < ROWS_PER_DEVICE; i++) {
          long ts = i * 500L;
          tablet.addTimestamp(i, ts);
          int v1 = 20 + i;
          tablet.addValue(i, 0, v1);

          Map<String, Object> row = new LinkedHashMap<>();
          row.put("timestamp", ts);
          row.put("temperature", v1);
          rows.add(row);
        }
        writer.write(tablet);

        Map<String, Object> dd = new LinkedHashMap<>();
        dd.put("deviceId", deviceId);
        dd.put("aligned", false);
        dd.put("measurements", Arrays.asList("temperature"));
        dd.put("dataTypes", Arrays.asList("INT32"));
        dd.put("rowCount", ROWS_PER_DEVICE);
        dd.put("rows", rows);
        deviceDataList.add(dd);
      }

      // === Aligned devices ===
      // Device 4: root.db1.aligned_d1 — aligned, INT32 + FLOAT + DOUBLE
      {
        String deviceId = "root.db1.aligned_d1";
        IMeasurementSchema s1 = new MeasurementSchema("speed", TSDataType.INT32);
        IMeasurementSchema s2 = new MeasurementSchema("power", TSDataType.FLOAT);
        IMeasurementSchema s3 = new MeasurementSchema("efficiency", TSDataType.DOUBLE);

        writer.registerAlignedTimeseries(deviceId, Arrays.asList(s1, s2, s3));

        Tablet tablet = new Tablet(deviceId, Arrays.asList(s1, s2, s3), ROWS_PER_DEVICE);
        List<Map<String, Object>> rows = new ArrayList<>();

        for (int i = 0; i < ROWS_PER_DEVICE; i++) {
          long ts = i * 2000L;
          tablet.addTimestamp(i, ts);
          int v1 = 1000 + i * 50;
          float v2 = 100.0f + i * 2.5f;
          double v3 = 0.85 + i * 0.005;
          tablet.addValue(i, 0, v1);
          tablet.addValue(i, 1, v2);
          tablet.addValue(i, 2, v3);

          Map<String, Object> row = new LinkedHashMap<>();
          row.put("timestamp", ts);
          row.put("speed", v1);
          row.put("power", v2);
          row.put("efficiency", v3);
          rows.add(row);
        }
        writer.write(tablet);

        Map<String, Object> dd = new LinkedHashMap<>();
        dd.put("deviceId", deviceId);
        dd.put("aligned", true);
        dd.put("measurements", Arrays.asList("speed", "power", "efficiency"));
        dd.put("dataTypes", Arrays.asList("INT32", "FLOAT", "DOUBLE"));
        dd.put("rowCount", ROWS_PER_DEVICE);
        dd.put("rows", rows);
        deviceDataList.add(dd);
      }

      // Device 5: root.db2.aligned_d1 — aligned, INT64 + BOOLEAN + TEXT
      {
        String deviceId = "root.db2.aligned_d1";
        IMeasurementSchema s1 = new MeasurementSchema("counter", TSDataType.INT64);
        IMeasurementSchema s2 = new MeasurementSchema("active", TSDataType.BOOLEAN);
        IMeasurementSchema s3 = new MeasurementSchema("label", TSDataType.TEXT);

        writer.registerAlignedTimeseries(deviceId, Arrays.asList(s1, s2, s3));

        Tablet tablet = new Tablet(deviceId, Arrays.asList(s1, s2, s3), ROWS_PER_DEVICE);
        List<Map<String, Object>> rows = new ArrayList<>();

        for (int i = 0; i < ROWS_PER_DEVICE; i++) {
          long ts = i * 3000L;
          tablet.addTimestamp(i, ts);
          long v1 = 10000L + i * 100;
          boolean v2 = i % 3 != 0;
          String v3 = "label_" + (i % 5);
          tablet.addValue(i, 0, v1);
          tablet.addValue(i, 1, v2);
          tablet.addValue(i, 2, v3);

          Map<String, Object> row = new LinkedHashMap<>();
          row.put("timestamp", ts);
          row.put("counter", v1);
          row.put("active", v2);
          row.put("label", v3);
          rows.add(row);
        }
        writer.write(tablet);

        Map<String, Object> dd = new LinkedHashMap<>();
        dd.put("deviceId", deviceId);
        dd.put("aligned", true);
        dd.put("measurements", Arrays.asList("counter", "active", "label"));
        dd.put("dataTypes", Arrays.asList("INT64", "BOOLEAN", "TEXT"));
        dd.put("rowCount", ROWS_PER_DEVICE);
        dd.put("rows", rows);
        deviceDataList.add(dd);
      }
    }

    Map<String, Object> meta = new LinkedHashMap<>();
    meta.put("type", "treeModel");
    meta.put("fileName", "comprehensive_tree_model.tsfile");
    meta.put("deviceCount", deviceDataList.size());
    meta.put("devices", deviceDataList);

    System.out.println(
        "Generated: comprehensive_tree_model.tsfile (" + deviceDataList.size() + " devices)");
    return meta;
  }
}
