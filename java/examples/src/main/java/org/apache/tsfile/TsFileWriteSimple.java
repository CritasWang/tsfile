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

package org.apache.tsfile;

import org.apache.tsfile.enums.TSDataType;
import org.apache.tsfile.exception.write.WriteProcessException;
import org.apache.tsfile.file.metadata.enums.TSEncoding;
import org.apache.tsfile.fileSystem.FSFactoryProducer;
import org.apache.tsfile.read.common.Path;
import org.apache.tsfile.write.TsFileWriter;
import org.apache.tsfile.write.record.Tablet;
import org.apache.tsfile.write.schema.IMeasurementSchema;
import org.apache.tsfile.write.schema.MeasurementSchema;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;

/**
 * Simple TsFile writer for interop testing. Accepts file path as command line argument and writes
 * sample data. Uses simple data types that can be easily read by C#.
 */
public class TsFileWriteSimple {

  public static void main(String[] args) {
    String path = args.length > 0 ? args[0] : "interop-test.tsfile";
    System.out.println("Writing TsFile: " + path);

    try {
      File f = FSFactoryProducer.getFSFactory().getFile(path);
      if (f.exists()) {
        Files.delete(f.toPath());
      }

      try (TsFileWriter tsFileWriter = new TsFileWriter(f)) {
        List<IMeasurementSchema> measurementSchemas = new ArrayList<>();
        // Use simple data types that C# can read
        measurementSchemas.add(new MeasurementSchema("s1", TSDataType.INT32, TSEncoding.PLAIN));
        measurementSchemas.add(new MeasurementSchema("s2", TSDataType.INT64, TSEncoding.PLAIN));
        measurementSchemas.add(new MeasurementSchema("s3", TSDataType.FLOAT, TSEncoding.PLAIN));
        measurementSchemas.add(new MeasurementSchema("s4", TSDataType.DOUBLE, TSEncoding.PLAIN));
        measurementSchemas.add(new MeasurementSchema("s5", TSDataType.BOOLEAN, TSEncoding.PLAIN));

        // Register device with simple name
        tsFileWriter.registerTimeseries(new Path("root.sg.device_1"), measurementSchemas);

        // Write data using tablet
        writeWithTablet(tsFileWriter, "root.sg.device_1", measurementSchemas, 100, 0, 0);
      }

      System.out.println("File written successfully!");
      System.out.println("File size: " + new File(path).length() + " bytes");
    } catch (Exception e) {
      System.err.println("Error writing TsFile: " + e.getMessage());
      e.printStackTrace();
      System.exit(1);
    }
  }

  private static void writeWithTablet(
      TsFileWriter tsFileWriter,
      String deviceId,
      List<IMeasurementSchema> schemas,
      long rowNum,
      long startTime,
      long startValue)
      throws IOException, WriteProcessException {
    Tablet tablet = new Tablet(deviceId, schemas);

    for (long r = 0; r < rowNum; r++, startValue++) {
      int row = tablet.getRowSize();
      tablet.addTimestamp(row, startTime++);
      // INT32
      tablet.addValue(schemas.get(0).getMeasurementName(), row, (int) r);
      // INT64
      tablet.addValue(schemas.get(1).getMeasurementName(), row, r * 10L);
      // FLOAT
      tablet.addValue(schemas.get(2).getMeasurementName(), row, r * 0.1f);
      // DOUBLE
      tablet.addValue(schemas.get(3).getMeasurementName(), row, r * 0.01);
      // BOOLEAN
      tablet.addValue(schemas.get(4).getMeasurementName(), row, r % 2 == 0);

      // Write when tablet is full
      if (tablet.getRowSize() == tablet.getMaxRowNumber()) {
        tsFileWriter.writeTree(tablet);
        tablet.reset();
      }
    }
    // Write remaining rows
    if (tablet.getRowSize() != 0) {
      tsFileWriter.writeTree(tablet);
      tablet.reset();
    }
  }
}
