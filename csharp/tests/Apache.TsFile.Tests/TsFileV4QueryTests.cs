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

using Apache.TsFile.Enums;
using Apache.TsFile.IO;
using Apache.TsFile.Schema;
using Xunit;

namespace Apache.TsFile.Tests;

/// <summary>
/// Tests for V4 query functionality.
/// </summary>
public class TsFileV4QueryTests
{
    [Fact]
    public void QueryV4File_ReturnsData()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            // Create and write V4 file
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i * 0.5);
                }

                writer.Write(tablet);
                writer.Close();
            }

            // Read and query
            using (var reader = new TsFileReader(tempFile))
            {
                Assert.Equal(4, reader.FileVersion);
                Assert.True(reader.Schemas.ContainsKey("test_table"));

                // Query the data
                var result = reader.Query("test_table");

                // Verify result is not empty
                Assert.NotNull(result);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_WithTimeRange_FiltersData()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 20; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i * 0.5);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                // Query with time range
                var result = reader.Query("test_table", startTime: 1500, endTime: 2000);

                Assert.NotNull(result);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    private static TableSchema CreateTestTableSchema(string tableName)
    {
        var tableSchema = new TableSchema(tableName);
        tableSchema.ColumnSchemas = new List<ColumnSchema>
        {
            new ColumnSchema("value", ColumnCategory.Field, TsDataType.Double,
                TsEncoding.Plain, CompressionType.Uncompressed),
        };

        foreach (var col in tableSchema.ColumnSchemas.Where(c => c.Category == ColumnCategory.Field))
        {
            tableSchema.AddMeasurement(new MeasurementSchema(col.Name, col.DataType,
                col.Encoding, col.Compression));
        }

        return tableSchema;
    }
}
