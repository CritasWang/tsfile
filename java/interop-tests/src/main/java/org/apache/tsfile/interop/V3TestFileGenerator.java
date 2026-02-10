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

// V3 API imports (org.apache.tsfile.* from tsfile:1.1.3)
// These imports will work when v3-interop profile is active
import org.apache.tsfile.enums.TSDataType;
import org.apache.tsfile.file.metadata.enums.CompressionType;
import org.apache.tsfile.file.metadata.enums.TSEncoding;
import org.apache.tsfile.write.TsFileWriter;
import org.apache.tsfile.write.record.TSRecord;
import org.apache.tsfile.write.record.datapoint.DataPoint;
import org.apache.tsfile.write.record.datapoint.DoubleDataPoint;
import org.apache.tsfile.write.record.datapoint.FloatDataPoint;
import org.apache.tsfile.write.record.datapoint.IntDataPoint;
import org.apache.tsfile.write.record.datapoint.LongDataPoint;
import org.apache.tsfile.write.schema.MeasurementSchema;

import java.io.File;
import java.io.IOException;

/**
 * Generates V3 format test files for C# interoperability testing. Uses TSFile 1.1.3 API
 * (org.apache.iotdb.tsfile package).
 *
 * <p>Note: This generator requires tsfile:1.1.3 dependency to be available. Run with: mvn
 * exec:java@generate-v3-files -Pv3-interop
 *
 * <p>IMPORTANT: This file will only compile when the v3-interop profile is active. The V3 and V4
 * APIs have different package names and cannot coexist in the same classpath.
 */
public class V3TestFileGenerator {

  private static final String DEVICE = "root.test.d0";
  private static final String SENSOR = "s0";
  private static final int VALUE_COUNT = 100;

  public static void main(String[] args) {
    try {
      String outputDir = args.length > 0 ? args[0] : "/tmp/interop-tests/java-v3";
      File dir = new File(outputDir);
      if (!dir.exists()) {
        dir.mkdirs();
      }

      System.out.println("Generating V3 test files in: " + outputDir);

      // Generate simple V3 test files
      generateSimpleV3Files(outputDir);

      System.out.println("V3 test file generation completed successfully.");

    } catch (Exception e) {
      System.err.println("Error generating V3 test files: " + e.getMessage());
      e.printStackTrace();
      System.exit(1);
    }
  }

  private static void generateSimpleV3Files(String outputDir) throws IOException {
    // Generate a few simple V3 test files with different data types
    generateV3File(outputDir, "v3_int32.tsfile", TSDataType.INT32, TSEncoding.PLAIN);
    generateV3File(outputDir, "v3_int64.tsfile", TSDataType.INT64, TSEncoding.RLE);
    generateV3File(outputDir, "v3_float.tsfile", TSDataType.FLOAT, TSEncoding.GORILLA);
    generateV3File(outputDir, "v3_double.tsfile", TSDataType.DOUBLE, TSEncoding.GORILLA);

    System.out.println("Generated 4 V3 test files");
  }

  private static void generateV3File(
      String outputDir, String fileName, TSDataType dataType, TSEncoding encoding)
      throws IOException {

    File file = new File(outputDir, fileName);
    System.out.println("Generating: " + fileName);

    try (TsFileWriter writer = new TsFileWriter(file)) {
      // Register schema
      MeasurementSchema schema =
          new MeasurementSchema(SENSOR, dataType, encoding, CompressionType.UNCOMPRESSED);
      writer.addMeasurement(schema);

      // Write data using TSRecord API (V3 style)
      for (int i = 0; i < VALUE_COUNT; i++) {
        TSRecord record = new TSRecord(i, DEVICE);
        DataPoint dataPoint = createDataPoint(dataType, i);
        record.addTuple(dataPoint);
        writer.write(record);
      }
    }

    System.out.println("  Generated: " + file.getAbsolutePath());
  }

  private static DataPoint createDataPoint(TSDataType dataType, int index) {
    switch (dataType) {
      case INT32:
        return new IntDataPoint(SENSOR, index);
      case INT64:
        return new LongDataPoint(SENSOR, (long) index);
      case FLOAT:
        return new FloatDataPoint(SENSOR, (float) index);
      case DOUBLE:
        return new DoubleDataPoint(SENSOR, (double) index);
      default:
        throw new IllegalArgumentException("Unsupported data type: " + dataType);
    }
  }
}
