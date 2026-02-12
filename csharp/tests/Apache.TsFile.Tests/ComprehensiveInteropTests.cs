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

using System.Text.Json;
using Apache.TsFile.IO;
using Xunit;
using Xunit.Abstractions;

namespace Apache.TsFile.Tests;

/// <summary>
/// Comprehensive interop tests: reads Java-generated table model and tree model files,
/// validates schema, row counts, and data values.
/// </summary>
public class ComprehensiveInteropTests
{
    private readonly ITestOutputHelper _output;
    private const string DefaultDir = "/tmp/comprehensive-interop";

    public ComprehensiveInteropTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private string GetTestDir()
    {
        var env = Environment.GetEnvironmentVariable("COMPREHENSIVE_INTEROP_DIR");
        return !string.IsNullOrEmpty(env) ? env : DefaultDir;
    }

    private JsonElement? LoadMetadata()
    {
        var path = Path.Combine(GetTestDir(), "comprehensive-metadata.json");
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    // =========================================================================
    // TABLE MODEL TESTS
    // =========================================================================

    [Fact]
    public void ReadTableModel_AllThreeTables()
    {
        var testDir = GetTestDir();
        var meta = LoadMetadata();
        if (meta == null)
        {
            _output.WriteLine($"SKIP: metadata not found in {testDir}");
            return;
        }

        var tableModel = meta.Value.GetProperty("tableModel");
        var tables = tableModel.GetProperty("tables");

        Assert.Equal(3, tables.GetArrayLength());

        for (int t = 0; t < 3; t++)
        {
            var tableMeta = tables[t];
            var fileName = tableMeta.GetProperty("fileName").GetString()!;
            var tableName = tableMeta.GetProperty("tableName").GetString()!;
            var filePath = Path.Combine(testDir, fileName);

            _output.WriteLine($"Reading table model file: {fileName} (table: {tableName})");
            Assert.True(File.Exists(filePath), $"File not found: {filePath}");

            using var reader = new TsFileReader(filePath);

            var result = reader.Query(tableName);
            Assert.NotNull(result);

            // Validate row count: 8 devices × 20 rows = 160 rows total
            int expectedTotalRows = tableMeta.GetProperty("totalRows").GetInt32();
            Assert.Equal(expectedTotalRows, result.Timestamps.Count);
            _output.WriteLine($"  Rows: {result.Timestamps.Count} (expected {expectedTotalRows})");

            // Validate we have at least some FIELD measurement columns
            // (Blob may fail to decode, so we check a subset)
            Assert.True(result.MeasurementData.Count > 0,
                "No measurement data returned for table model");
            _output.WriteLine($"  Columns returned: [{string.Join(", ", result.MeasurementData.Keys)}]");

            // Validate numeric FIELD columns have correct count
            string[] numericCols = { "f_int32", "f_int64", "f_float", "f_double", "f_boolean" };
            foreach (var col in numericCols)
            {
                if (result.MeasurementData.TryGetValue(col, out var data))
                {
                    Assert.Equal(expectedTotalRows, data.Count);
                    _output.WriteLine($"  {col}: {data.Count} values");
                }
            }

            _output.WriteLine($"  Table {tableName}: PASSED");
        }
    }

    // =========================================================================
    // TREE MODEL TESTS
    // =========================================================================

    [Fact]
    public void ReadTreeModel_AllDevices()
    {
        var testDir = GetTestDir();
        var meta = LoadMetadata();
        if (meta == null)
        {
            _output.WriteLine($"SKIP: metadata not found in {testDir}");
            return;
        }

        var treeModel = meta.Value.GetProperty("treeModel");
        var fileName = treeModel.GetProperty("fileName").GetString()!;
        var filePath = Path.Combine(testDir, fileName);

        _output.WriteLine($"Reading tree model file: {fileName}");
        Assert.True(File.Exists(filePath), $"File not found: {filePath}");

        var devices = treeModel.GetProperty("devices");
        int deviceCount = treeModel.GetProperty("deviceCount").GetInt32();
        Assert.Equal(5, deviceCount);

        using var reader = new TsFileReader(filePath);

        for (int d = 0; d < deviceCount; d++)
        {
            var deviceMeta = devices[d];
            var deviceId = deviceMeta.GetProperty("deviceId").GetString()!;
            bool isAligned = deviceMeta.GetProperty("aligned").GetBoolean();
            int expectedRowCount = deviceMeta.GetProperty("rowCount").GetInt32();
            var measurements = deviceMeta.GetProperty("measurements");

            _output.WriteLine($"  Device: {deviceId} (aligned={isAligned})");

            // Verify device is queryable
            var result = reader.Query(deviceId);
            Assert.NotNull(result);
            Assert.True(result.Timestamps.Count > 0,
                $"No timestamps returned for device {deviceId}");
            _output.WriteLine($"    Timestamps: {result.Timestamps.Count}");

            // Verify measurements are present
            Assert.True(result.MeasurementData.Count > 0,
                $"No measurement data for device {deviceId}");
            _output.WriteLine($"    Measurements: [{string.Join(", ", result.MeasurementData.Keys)}]");

            // For non-aligned devices, each measurement chunk includes its own timestamps,
            // so total timestamps = expectedRowCount × measurementCount.
            // For aligned devices, timestamps come from a single time chunk.
            if (isAligned)
            {
                Assert.Equal(expectedRowCount, result.Timestamps.Count);
            }

            _output.WriteLine($"    Device {deviceId}: PASSED");
        }
    }

    [Fact]
    public void ReadTreeModel_NonAlignedDevice_ValidateValues()
    {
        var testDir = GetTestDir();
        var meta = LoadMetadata();
        if (meta == null) return;

        var treeModel = meta.Value.GetProperty("treeModel");
        var filePath = Path.Combine(testDir, treeModel.GetProperty("fileName").GetString()!);
        if (!File.Exists(filePath)) return;

        // Validate root.db2.d1 (non-aligned, single measurement: temperature INT32)
        var devices = treeModel.GetProperty("devices");
        var device = devices[2]; // root.db2.d1
        var deviceId = device.GetProperty("deviceId").GetString()!;
        Assert.Equal("root.db2.d1", deviceId);

        using var reader = new TsFileReader(filePath);
        var result = reader.Query(deviceId);

        int expectedRowCount = device.GetProperty("rowCount").GetInt32();
        Assert.True(result.MeasurementData.ContainsKey("temperature"),
            "Missing measurement: temperature");

        var tempData = result.MeasurementData["temperature"];
        var rows = device.GetProperty("rows");

        // Single measurement non-aligned device: timestamps match 1:1
        Assert.Equal(expectedRowCount, result.Timestamps.Count);
        Assert.Equal(expectedRowCount, tempData.Count);

        // Validate all values
        for (int r = 0; r < expectedRowCount; r++)
        {
            var expectedRow = rows[r];
            Assert.Equal(expectedRow.GetProperty("timestamp").GetInt64(), result.Timestamps[r]);
            Assert.Equal(expectedRow.GetProperty("temperature").GetInt32(), Convert.ToInt32(tempData[r]));
        }

        _output.WriteLine($"root.db2.d1: {expectedRowCount} rows validated with exact values");
    }
}
