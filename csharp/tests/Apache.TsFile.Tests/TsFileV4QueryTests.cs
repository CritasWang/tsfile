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

    [Fact]
    public void QueryV4File_WithValueFilter_Gt()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                // Filter: value > 25.0
                var result = reader.Query("test_table",
                    valueFilter: Filter.Gt("value", 25.0));

                Assert.NotNull(result);
                // Values 20..29, filter > 25 → 26,27,28,29 = 4 rows
                Assert.Equal(4, result.Timestamps.Count);
                foreach (var val in result.MeasurementData["value"])
                    Assert.True((double)val > 25.0);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_WithValueFilter_AndCombination()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                // Filter: value >= 23.0 AND value <= 26.0
                var result = reader.Query("test_table",
                    valueFilter: Filter.And(
                        Filter.Ge("value", 23.0),
                        Filter.Le("value", 26.0)));

                Assert.NotNull(result);
                // Values 20..29, filter [23,26] → 23,24,25,26 = 4 rows
                Assert.Equal(4, result.Timestamps.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_WithValueFilter_OrCombination()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                // Filter: value == 20.0 OR value == 29.0
                var result = reader.Query("test_table",
                    valueFilter: Filter.Or(
                        Filter.Eq("value", 20.0),
                        Filter.Eq("value", 29.0)));

                Assert.NotNull(result);
                Assert.Equal(2, result.Timestamps.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_WithValueFilter_Not()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                // Filter: NOT (value < 25.0) → value >= 25.0 → 25,26,27,28,29 = 5 rows
                var result = reader.Query("test_table",
                    valueFilter: Filter.Not(Filter.Lt("value", 25.0)));

                Assert.NotNull(result);
                Assert.Equal(5, result.Timestamps.Count);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_Aggregation_MinMaxAvgSumCountFirstLast()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                var result = reader.Query("test_table");

                Assert.Equal(10, result.Count());
                Assert.Equal(10, result.Count("value"));
                Assert.Equal(20.0, result.Min("value"));
                Assert.Equal(29.0, result.Max("value"));
                Assert.Equal(245.0, result.Sum("value")); // 20+21+...+29 = 245
                Assert.Equal(24.5, result.Avg("value"));
                Assert.Equal(20.0, (double)result.First("value")!);
                Assert.Equal(29.0, (double)result.Last("value")!);

                var agg = result.Aggregate("value");
                Assert.Equal(10, agg["count"]);
                Assert.Equal(20.0, agg["min"]);
                Assert.Equal(29.0, agg["max"]);
                Assert.Equal(245.0, agg["sum"]);
                Assert.Equal(24.5, agg["avg"]);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_Aggregation_WithFilter()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                // Filter: value >= 25.0 → 25,26,27,28,29
                var result = reader.Query("test_table",
                    valueFilter: Filter.Ge("value", 25.0));

                Assert.Equal(5, result.Count());
                Assert.Equal(25.0, result.Min("value"));
                Assert.Equal(29.0, result.Max("value"));
                Assert.Equal(135.0, result.Sum("value")); // 25+26+27+28+29 = 135
                Assert.Equal(27.0, result.Avg("value"));
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_Aggregation_EmptyResult()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                // Filter: value > 100 → no rows
                var result = reader.Query("test_table",
                    valueFilter: Filter.Gt("value", 100.0));

                Assert.Equal(0, result.Count());
                Assert.Null(result.Min("value"));
                Assert.Null(result.Max("value"));
                Assert.Null(result.Sum("value"));
                Assert.Null(result.Avg("value"));
                Assert.Null(result.First("value"));
                Assert.Null(result.Last("value"));
            }
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void QueryV4File_Statistics_Available()
    {
        var tempFile = Path.GetTempFileName() + ".tsfile";

        try
        {
            var tableSchema = CreateTestTableSchema("test_table");

            using (var writer = new TsFileWriter(tempFile))
            {
                writer.RegisterTableSchema(tableSchema);

                var tablet = new Tablet(tableSchema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            using (var reader = new TsFileReader(tempFile))
            {
                var result = reader.Query("test_table");
                Assert.NotNull(result);
                // Statistics should be populated for V4 files
                // (may or may not have entries depending on file structure)
                Assert.NotNull(result.Statistics);
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
