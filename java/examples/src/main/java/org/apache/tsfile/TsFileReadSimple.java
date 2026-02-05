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
import org.apache.tsfile.file.metadata.IDeviceID;
import org.apache.tsfile.read.TsFileReader;
import org.apache.tsfile.read.TsFileSequenceReader;
import org.apache.tsfile.read.common.Path;
import org.apache.tsfile.read.expression.QueryExpression;
import org.apache.tsfile.read.query.dataset.QueryDataSet;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * Simple TsFile reader for interop testing. Accepts file path as command line argument and reads
 * all data.
 */
public class TsFileReadSimple {

  public static void main(String[] args) throws IOException {
    if (args.length < 1) {
      System.err.println("Usage: TsFileReadSimple <file_path>");
      System.exit(1);
    }

    String path = args[0];
    System.out.println("Reading TsFile: " + path);

    try (TsFileSequenceReader reader = new TsFileSequenceReader(path)) {
      System.out.println("File version: " + reader.readVersionNumber());

      // Get all device IDs
      List<IDeviceID> devices = reader.getAllDevices();
      System.out.println("Found " + devices.size() + " device(s):");

      for (IDeviceID device : devices) {
        System.out.println("  - " + device);
        Map<String, TSDataType> measurements = reader.getMeasurement(device);
        System.out.println("    Measurements: " + measurements.keySet());
      }

      // Try to read data
      try (TsFileReader tsFileReader = new TsFileReader(reader)) {
        for (IDeviceID device : devices) {
          Map<String, TSDataType> measurements = reader.getMeasurement(device);
          ArrayList<Path> paths = new ArrayList<>();
          for (String measurement : measurements.keySet()) {
            paths.add(new Path(device, measurement, true));
          }

          if (!paths.isEmpty()) {
            QueryExpression queryExpression = QueryExpression.create(paths, null);
            QueryDataSet queryDataSet = tsFileReader.query(queryExpression);

            int count = 0;
            while (queryDataSet.hasNext() && count < 5) {
              System.out.println("    Row: " + queryDataSet.next());
              count++;
            }

            // Count remaining rows
            while (queryDataSet.hasNext()) {
              queryDataSet.next();
              count++;
            }

            System.out.println("    Total rows: " + count);
          }
        }
      }

      System.out.println("File read successfully!");
    }
  }
}
