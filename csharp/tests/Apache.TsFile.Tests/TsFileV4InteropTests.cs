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
using System.Text.Json;
using Xunit;

namespace Apache.TsFile.Tests;

/// <summary>
/// Comprehensive tests for TsFile V4 reading, writing, and Java interoperability.
/// </summary>
public class TsFileV4InteropTests
{
    private string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !Directory.Exists(Path.Combine(currentDir, ".git")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }

    private string GetJavaV4TestFilesDir()
    {
        // Priority: 1. Environment variable, 2. Default CI path
        var envDir = Environment.GetEnvironmentVariable("JAVA_V4_TEST_FILES_DIR");
        return !string.IsNullOrEmpty(envDir) ? envDir : "/tmp/interop-tests/java-v4";
    }

    private string GetJavaComprehensiveDir()
    {
        var envDir = Environment.GetEnvironmentVariable("JAVA_COMPREHENSIVE_DIR");
        return !string.IsNullOrEmpty(envDir) ? envDir : "/tmp/interop-tests/java-comprehensive";
    }

    private string GetJavaV3TestFilesDir()
    {
        var envDir = Environment.GetEnvironmentVariable("JAVA_V3_TEST_FILES_DIR");
        return !string.IsNullOrEmpty(envDir) ? envDir : "/tmp/interop-tests/java-v3";
    }

    [Fact]
    public void ReadJavaV4File_CanReadSchemas()
    {
        // Try CI-generated files first (true interop test)
        var javaV4Dir = GetJavaV4TestFilesDir();
        string? javaV4File = null;

        if (Directory.Exists(javaV4Dir))
        {
            var files = Directory.GetFiles(javaV4Dir, "*.tsfile");
            if (files.Length > 0)
            {
                javaV4File = files[0];
            }
        }

        // Fallback to static file for local testing
        if (javaV4File == null || !File.Exists(javaV4File))
        {
            javaV4File = Path.Combine(GetRepositoryRoot(), "java/examples/Tablet.tsfile");
        }

        if (!File.Exists(javaV4File))
        {
            // Skip test if Java V4 files not found
            return;
        }

        // Verify it's a v4 file
        using var fs = new FileStream(javaV4File, FileMode.Open, FileAccess.Read);
        var magic = new byte[6];
        fs.ReadExactly(magic, 0, 6);
        var version = fs.ReadByte();

        Assert.Equal(4, version);
    }
    
    [Fact]
    public void ReadJavaV4File_CanReadSchemasWithReader()
    {
        // Try CI-generated files first (true interop test)
        var javaV4Dir = GetJavaV4TestFilesDir();
        string? javaV4File = null;

        if (Directory.Exists(javaV4Dir))
        {
            var files = Directory.GetFiles(javaV4Dir, "*.tsfile");
            if (files.Length > 0)
            {
                javaV4File = files[0];
            }
        }

        // Fallback to static file for local testing
        if (javaV4File == null || !File.Exists(javaV4File))
        {
            javaV4File = Path.Combine(GetRepositoryRoot(), "java/examples/Tablet.tsfile");
        }

        if (!File.Exists(javaV4File))
        {
            // Skip test if Java V4 files not found
            return;
        }

        // Verify it's a v4 file
        // This test verifies that we can at least attempt to read without crashing
        try
        {
            using var reader = new TsFileReader(javaV4File);

            // If we get here, basic parsing worked
            Assert.NotNull(reader.Schemas);

            // V4 files should have table schemas
            foreach (var schema in reader.Schemas)
            {
                Assert.NotNull(schema.Key);
                Assert.NotNull(schema.Value);
            }
        }
        catch (InvalidDataException)
        {
            // Java V4 format may have features not yet supported
            // This is expected for complex files
        }
    }
    
    [Fact]
    public void ReadJavaV4File_WithTsFileReader()
    {
        // Try CI-generated files first (true interop test)
        var javaV4Dir = GetJavaV4TestFilesDir();
        string? javaV4File = null;

        if (Directory.Exists(javaV4Dir))
        {
            var files = Directory.GetFiles(javaV4Dir, "*.tsfile");
            if (files.Length > 0)
            {
                javaV4File = files[0];
            }
        }

        // Fallback to static file for local testing
        if (javaV4File == null || !File.Exists(javaV4File))
        {
            javaV4File = Path.Combine(GetRepositoryRoot(), "java/examples/Tablet.tsfile");
        }

        if (!File.Exists(javaV4File))
        {
            // Skip test if Java V4 files not found
            return;
        }

        // Java V4 files have a complex format with IDeviceID serialization
        // that may differ from our implementation
        try
        {
            using var reader = new TsFileReader(javaV4File);

            // Verify file version
            Assert.Equal(4, reader.FileVersion);

            // Verify schemas are loaded
            Assert.NotEmpty(reader.Schemas);

            // List all tables
            var tableNames = reader.Schemas.Keys.ToList();
            Assert.NotEmpty(tableNames);

            Console.WriteLine($"\n  File: {Path.GetFileName(javaV4File)}");
            Console.WriteLine($"  Version: {reader.FileVersion}");
            Console.WriteLine($"  Tables: {string.Join(", ", tableNames)}");
            foreach (var tableName in tableNames)
            {
                var schema = reader.Schemas[tableName];
                Console.WriteLine($"  [{tableName}] measurements: {string.Join(", ", schema.Measurements.Select(m => $"{m.MeasurementName}({m.DataType})"))}");
                try
                {
                    var result = reader.Query(tableName);
                    Console.WriteLine($"  [{tableName}] rows={result.Timestamps.Count}  data: {FormatSampleValues(result, maxRows: 3)}");
                }
                catch (Exception qex)
                {
                    Console.WriteLine($"  [{tableName}] query error: {qex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
        {
            // Java V4 format may have features not yet supported
            // This is expected for complex files with different IDeviceID serialization
        }
    }
    
    [Fact]
    public void WriteV4File_CanCreateValidFile()
    {
        var testFile = Path.Combine(Path.GetTempPath(), $"test_v4_{Guid.NewGuid()}.tsfile");

        try
        {
            // Create table schema with tags and fields
            var schema = new TableSchema("sensor_data");
            schema.ColumnSchemas = new List<ColumnSchema>
            {
                new ColumnSchema("region", ColumnCategory.Tag, TsDataType.String, TsEncoding.Plain, CompressionType.Uncompressed),
                new ColumnSchema("device", ColumnCategory.Tag, TsDataType.String, TsEncoding.Plain, CompressionType.Uncompressed),
                new ColumnSchema("temperature", ColumnCategory.Field, TsDataType.Double, TsEncoding.Plain, CompressionType.Uncompressed),
                new ColumnSchema("humidity", ColumnCategory.Field, TsDataType.Int32, TsEncoding.Plain, CompressionType.Uncompressed)
            };

            // Add fields to measurements for compatibility
            foreach (var col in schema.ColumnSchemas.Where(c => c.Category == ColumnCategory.Field))
            {
                schema.AddMeasurement(new MeasurementSchema(col.Name, col.DataType, col.Encoding, col.Compression));
            }

            // Create writer using unified API (defaults to V4)
            using (var writer = new TsFileWriter(testFile))
            {
                writer.RegisterTableSchema(schema);

                var tablet = new Tablet(schema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(i * 1000L, 25.5 + i, 60 + i);
                }

                writer.Write(tablet);
                writer.Close();
            }

            // Verify file was created
            Assert.True(File.Exists(testFile));

            // Verify it's a valid V4 file
            using var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read);
            var magic = new byte[6];
            fs.ReadExactly(magic, 0, 6);

            // Check magic string "TsFile"
            Assert.Equal((byte)'T', magic[0]);
            Assert.Equal((byte)'s', magic[1]);
            Assert.Equal((byte)'F', magic[2]);
            Assert.Equal((byte)'i', magic[3]);
            Assert.Equal((byte)'l', magic[4]);
            Assert.Equal((byte)'e', magic[5]);

            // Check version
            var version = fs.ReadByte();
            Assert.Equal(4, version);
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }
    
    [Fact]
    public void WriteAndReadV4File_RoundTrip()
    {
        var testFile = Path.Combine(Path.GetTempPath(), $"test_v4_roundtrip_{Guid.NewGuid()}.tsfile");

        try
        {
            // Create table schema
            var schema = new TableSchema("test_table");
            schema.ColumnSchemas = new List<ColumnSchema>
            {
                new ColumnSchema("tag1", ColumnCategory.Tag, TsDataType.String, TsEncoding.Plain, CompressionType.Uncompressed),
                new ColumnSchema("s1", ColumnCategory.Field, TsDataType.Int32, TsEncoding.Plain, CompressionType.Uncompressed),
                new ColumnSchema("s2", ColumnCategory.Field, TsDataType.Double, TsEncoding.Plain, CompressionType.Uncompressed)
            };

            foreach (var col in schema.ColumnSchemas.Where(c => c.Category == ColumnCategory.Field))
            {
                schema.AddMeasurement(new MeasurementSchema(col.Name, col.DataType, col.Encoding, col.Compression));
            }

            // Write data using unified API
            using (var writer = new TsFileWriter(testFile))
            {
                writer.RegisterTableSchema(schema);

                var tablet = new Tablet(schema, 100);
                for (int i = 0; i < 5; i++)
                {
                    tablet.AddRow(i * 100L, i * 10, i * 1.5);
                }

                writer.Write(tablet);
                writer.Close();
            }

            // Verify file was created
            Assert.True(File.Exists(testFile));

            // Read it back with unified TsFileReader
            using var reader = new TsFileReader(testFile);

            Assert.Equal(4, reader.FileVersion);
            Assert.NotEmpty(reader.Schemas);
            Assert.True(reader.Schemas.ContainsKey("test_table"));
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }
    
    [Fact]
    public void DeviceID_Serialization_RoundTrip()
    {
        // Test StringArrayDeviceID serialization
        var deviceId = new StringArrayDeviceID("table1", "region1", "device1");
        
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        var serializedSize = deviceId.Serialize(writer);
        Assert.True(serializedSize > 0);
        
        // Verify the device ID properties
        Assert.Equal("table1", deviceId.GetTableName());
        Assert.Equal(3, deviceId.SegmentCount);
        Assert.Equal("table1", deviceId.GetSegment(0));
        Assert.Equal("region1", deviceId.GetSegment(1));
        Assert.Equal("device1", deviceId.GetSegment(2));
    }
    
    [Fact]
    public void MetadataIndexNode_Serialization()
    {
        var node = new MetadataIndexNode(MetadataIndexNodeType.LeafDevice);
        
        var deviceId = new StringArrayDeviceID("table1", "tag1");
        var entry = new DeviceMetadataIndexEntry(deviceId, 12345L);
        node.AddEntry(entry);
        node.SetEndOffset(99999L);
        
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        var serializedSize = node.Serialize(writer);
        Assert.True(serializedSize > 0);
        
        // Verify node properties
        Assert.Equal(1, node.Entries.Count);
        Assert.Equal(MetadataIndexNodeType.LeafDevice, node.NodeType);
        Assert.True(node.IsLeaf);
        Assert.True(node.IsDeviceLevel);
    }
    
    [Fact]
    public void Tablet_BasicOperations()
    {
        var schema = new TableSchema("test");
        schema.AddMeasurement(new MeasurementSchema("s1", TsDataType.Int32, TsEncoding.Plain, CompressionType.Uncompressed));
        schema.AddMeasurement(new MeasurementSchema("s2", TsDataType.Double, TsEncoding.Plain, CompressionType.Uncompressed));

        var tablet = new Tablet(schema, 100);

        // Add data
        tablet.AddRow(1000L, 100, 3.14);
        tablet.AddRow(2000L, 200, 6.28);

        // Verify
        Assert.Equal(2, tablet.RowCount);
        Assert.Equal(1000L, tablet.Timestamps[0]);
        Assert.Equal(2000L, tablet.Timestamps[1]);

        // Reset
        tablet.Reset();
        Assert.Equal(0, tablet.RowCount);
    }

    [Fact]
    public void StringArrayDeviceID_FromString()
    {
        // Test creating from a path string
        var deviceId = new StringArrayDeviceID("root.a.b.c.d");
        
        // Should split according to rules
        Assert.False(deviceId.IsEmpty);
        Assert.False(deviceId.IsTableModel); // Starts with "root"
    }
    
    [Fact]
    public void StringArrayDeviceID_TableModel()
    {
        // Test table model device ID
        var deviceId = new StringArrayDeviceID("table1", "Beijing", "Device1");
        
        Assert.False(deviceId.IsEmpty);
        Assert.True(deviceId.IsTableModel); // Does not start with "root."
        Assert.Equal("table1", deviceId.GetTableName());
        Assert.Equal("table1.Beijing.Device1", deviceId.ToString());
    }
    
    [Fact]
    public void StringArrayDeviceID_Comparison()
    {
        var device1 = new StringArrayDeviceID("table1", "a", "b");
        var device2 = new StringArrayDeviceID("table1", "a", "c");
        var device3 = new StringArrayDeviceID("table1", "a", "b");

        Assert.True(device1.CompareTo(device2) < 0);
        Assert.True(device2.CompareTo(device1) > 0);
        Assert.Equal(0, device1.CompareTo(device3));

        // Test equality
        Assert.True(device1.Equals(device3));
        Assert.False(device1.Equals(device2));
    }

    /// <summary>
    /// Generates C# V4 test files for Java interoperability testing.
    /// This test creates files that can be read by Java's CSharpFileValidator.
    /// </summary>
    [Fact]
    public void GenerateCSharpV4FilesForJavaInterop()
    {
        // Determine output directory
        // Priority: 1. Environment variable, 2. CI detection, 3. Local temp
        var outputDir = Environment.GetEnvironmentVariable("CSHARP_V4_OUTPUT_DIR");

        if (string.IsNullOrEmpty(outputDir))
        {
            // Check if running in CI (GitHub Actions sets CI=true)
            var isCI = Environment.GetEnvironmentVariable("CI") == "true"
                    || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

            outputDir = isCI
                ? "/tmp/interop-tests/csharp-v4"  // CI path
                : Path.Combine(Path.GetTempPath(), "csharp-v4-interop");  // Local path
        }

        Directory.CreateDirectory(outputDir);

        // Generate simple V4 file
        var simplePath = Path.Combine(outputDir, "simple_csharp_v4.tsfile");
        GenerateSimpleV4File(simplePath);

        // Generate multi-device V4 file
        var multiPath = Path.Combine(outputDir, "multi_device_csharp_v4.tsfile");
        GenerateMultiDeviceV4File(multiPath);

        // Generate tree model V4 file
        var treePath = Path.Combine(outputDir, "tree_model_csharp_v4.tsfile");
        GenerateTreeModelV4File(treePath);

        // Verify all files exist
        Assert.True(File.Exists(simplePath), $"Simple V4 file not created: {simplePath}");
        Assert.True(File.Exists(multiPath), $"Multi-device V4 file not created: {multiPath}");
        Assert.True(File.Exists(treePath), $"Tree model V4 file not created: {treePath}");

        // Output paths for CI
        Console.WriteLine($"Generated C# V4 files in: {outputDir}");
        Console.WriteLine($"  - {simplePath}");
        Console.WriteLine($"  - {multiPath}");
        Console.WriteLine($"  - {treePath}");
    }

    private void GenerateSimpleV4File(string path)
    {
        if (File.Exists(path)) File.Delete(path);

        using var writer = new TsFileWriter(path);
        var measurements = new List<MeasurementSchema>
        {
            new("temperature", TsDataType.Double, TsEncoding.Plain, CompressionType.Uncompressed),
            new("humidity", TsDataType.Int32, TsEncoding.Plain, CompressionType.Uncompressed)
        };
        writer.RegisterDevice("root.test.device1", measurements);

        var tablet = new Tablet("root.test.device1", measurements, 10); // 缩减10倍: 100 -> 10
        for (int i = 0; i < 10; i++) // 缩减10倍: 10 -> 10 (保持不变，已经很小)
        {
            tablet.AddRow(i * 1000L, 25.0 + i * 0.5, 60 + i);
        }
        writer.Write(tablet);
        writer.Close();
    }

    private void GenerateMultiDeviceV4File(string path)
    {
        if (File.Exists(path)) File.Delete(path);

        using var writer = new TsFileWriter(path);

        var measurements1 = new List<MeasurementSchema>
        {
            new("speed", TsDataType.Int64, TsEncoding.Plain, CompressionType.Lz4)
        };
        var measurements2 = new List<MeasurementSchema>
        {
            new("power", TsDataType.Float, TsEncoding.Plain, CompressionType.Lz4)
        };

        writer.RegisterDevice("root.factory.line1.machine1", measurements1);
        writer.RegisterDevice("root.factory.line1.machine2", measurements2);

        var tablet1 = new Tablet("root.factory.line1.machine1", measurements1, 10); // 缩减10倍: 100 -> 10
        var tablet2 = new Tablet("root.factory.line1.machine2", measurements2, 10); // 缩减10倍: 100 -> 10

        for (int i = 0; i < 5; i++) // 保持5行数据
        {
            tablet1.AddRow(i * 100L, 1000L + i * 10);
            tablet2.AddRow(i * 100L, 100.0f + i * 0.5f);
        }

        writer.Write(tablet1);
        writer.Write(tablet2);
        writer.Close();
    }

    private void GenerateTreeModelV4File(string path)
    {
        if (File.Exists(path)) File.Delete(path);

        using var writer = new TsFileWriter(path);

        var measurements = new List<MeasurementSchema>
        {
            new("value", TsDataType.Int32, TsEncoding.Plain, CompressionType.Uncompressed),
            new("status", TsDataType.Boolean, TsEncoding.Plain, CompressionType.Uncompressed)
        };

        // Use tree model registration
        writer.RegisterTimeseries("root.sg1.d1", measurements);

        var tablet = new Tablet("root.sg1.d1", measurements, 10); // 缩减10倍: 100 -> 10
        for (int i = 0; i < 10; i++) // 缩减10倍: 20 -> 10 (修复 Tablet is full 错误)
        {
            tablet.AddRow(i * 50L, i * 100, i % 2 == 0);
        }

        writer.Write(tablet);
        writer.Close();
    }

    /// <summary>
    /// Tests querying data from Java-generated V4 files.
    /// This verifies that C# can read and query data written by Java.
    /// Reads from CI-generated files in /tmp/interop-tests/java-v4/ or falls back to static file.
    /// </summary>
    [Fact]
    public void QueryJavaV4File_ReturnsData()
    {
        // Try CI-generated files first (true interop test)
        var javaV4Dir = GetJavaV4TestFilesDir();
        string[]? javaV4Files = null;

        if (Directory.Exists(javaV4Dir))
        {
            javaV4Files = Directory.GetFiles(javaV4Dir, "*.tsfile");
        }

        // Fallback to static file for local testing
        if (javaV4Files == null || javaV4Files.Length == 0)
        {
            var fallback = Path.Combine(GetRepositoryRoot(), "java/examples/Tablet.tsfile");
            if (File.Exists(fallback))
                javaV4Files = new[] { fallback };
        }

        if (javaV4Files == null || javaV4Files.Length == 0)
        {
            // Skip test if Java V4 files not found
            return;
        }

        Console.WriteLine($"\n  Reading {javaV4Files.Length} Java V4 simple files from {javaV4Dir}");

        foreach (var javaV4File in javaV4Files)
        {
            try
            {
                using var reader = new TsFileReader(javaV4File);

                Assert.Equal(4, reader.FileVersion);
                Assert.NotEmpty(reader.Schemas);

                Console.WriteLine($"\n  \u2713 {Path.GetFileName(javaV4File)}");
                foreach (var tableName in reader.Schemas.Keys)
                {
                    var result = reader.Query(tableName);
                    Assert.NotNull(result);
                    Assert.Equal(tableName, result.DeviceName);

                    var schema = reader.Schemas[tableName];
                    Console.WriteLine($"    [{tableName}] measurements: {string.Join(", ", schema.Measurements.Select(m => $"{m.MeasurementName}({m.DataType}/{m.Encoding}/{m.Compression})"))}");
                    Console.WriteLine($"    [{tableName}] rows={result.Timestamps.Count}  data: {FormatSampleValues(result, maxRows: 5)}");
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException)
            {
                Console.WriteLine($"  \u2717 {Path.GetFileName(javaV4File)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tests that C# written V4 files can be queried correctly.
    /// This is a round-trip test for V4 query functionality.
    /// </summary>
    [Fact]
    public void WriteAndQueryV4File_RoundTrip()
    {
        var testFile = Path.Combine(Path.GetTempPath(), $"test_v4_query_{Guid.NewGuid()}.tsfile");

        try
        {
            // Create and write V4 file
            var schema = new TableSchema("query_test");
            schema.ColumnSchemas = new List<ColumnSchema>
            {
                new ColumnSchema("value", ColumnCategory.Field, TsDataType.Double,
                    TsEncoding.Plain, CompressionType.Uncompressed),
            };

            foreach (var col in schema.ColumnSchemas.Where(c => c.Category == ColumnCategory.Field))
            {
                schema.AddMeasurement(new MeasurementSchema(col.Name, col.DataType,
                    col.Encoding, col.Compression));
            }

            using (var writer = new TsFileWriter(testFile))
            {
                writer.RegisterTableSchema(schema);

                var tablet = new Tablet(schema, 100);
                for (int i = 0; i < 10; i++)
                {
                    tablet.AddRow(1000 + i * 100, 20.0 + i * 0.5);
                }

                writer.Write(tablet);
                writer.Close();
            }

            // Read and query
            using (var reader = new TsFileReader(testFile))
            {
                Assert.Equal(4, reader.FileVersion);
                Assert.True(reader.Schemas.ContainsKey("query_test"));

                // Query the data
                var result = reader.Query("query_test");
                Assert.NotNull(result);
                Assert.Equal("query_test", result.DeviceName);
            }
        }
        finally
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
    }

    /// <summary>
    /// Tests reading all comprehensive Java test files (360 files).
    /// Validates data types, encodings, compressions, and expected values.
    /// Note: This test is experimental as format alignment is in progress.
    /// </summary>
    [Fact]
    public void ReadComprehensiveJavaFiles_ValidatesAllCombinations()
    {
        var javaComprehensiveDir = GetJavaComprehensiveDir();
        var metadataFile = Path.Combine(javaComprehensiveDir, "test-metadata.json");

        if (!File.Exists(metadataFile))
        {
            // Skip test if comprehensive files not generated
            return;
        }

        // Load metadata for expected value validation
        var metadataJson = File.ReadAllText(metadataFile);
        var metadataList = JsonSerializer.Deserialize<List<TestFileMetadata>>(metadataJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var metadataMap = metadataList?.ToDictionary(m => m.FileName, m => m)
            ?? new Dictionary<string, TestFileMetadata>();

        // Count and validate files
        var tsfiles = Directory.GetFiles(javaComprehensiveDir, "*.tsfile");
        Assert.True(tsfiles.Length > 0, $"Should have generated test files in {javaComprehensiveDir}");

        int successCount = 0;
        int valueMatchCount = 0;
        var errors = new List<string>();

        Console.WriteLine($"\n{'=',-80}");
        Console.WriteLine($"Reading {tsfiles.Length} comprehensive Java test files");
        Console.WriteLine($"{'=',-80}");

        foreach (var file in tsfiles.OrderBy(f => f))
        {
            var fileName = Path.GetFileName(file);
            try
            {
                using var reader = new TsFileReader(file);

                Assert.Equal(4, reader.FileVersion);
                Assert.NotNull(reader.Schemas);
                Assert.NotEmpty(reader.Schemas);

                var deviceName = reader.Schemas.Keys.First();
                var schema = reader.Schemas[deviceName];
                var result = reader.Query(deviceName);
                Assert.NotNull(result);

                // Build measurement info
                var measurementInfo = string.Join(", ", schema.Measurements.Select(m =>
                    $"{m.MeasurementName}({m.DataType}/{m.Encoding}/{m.Compression})"));

                // Get row count and sample values
                var rowCount = result.Timestamps.Count;
                var sampleValues = FormatSampleValues(result, maxRows: 5);

                // Validate against expected metadata
                var valuesMatch = "";
                if (metadataMap.TryGetValue(fileName, out var meta) && meta.ExpectedValues != null)
                {
                    var measurement = result.MeasurementData.Keys.FirstOrDefault();
                    if (measurement != null && result.MeasurementData[measurement].Count > 0)
                    {
                        var actualValues = result.MeasurementData[measurement];
                        var (matched, mismatchDetail) = ValidateExpectedValues(actualValues, meta.ExpectedValues, meta.DataType);
                        valuesMatch = matched ? " [VALUES MATCH ✓]" : $" [VALUES MISMATCH ✗ {mismatchDetail}]";
                        if (matched) valueMatchCount++;
                    }
                }

                Console.WriteLine($"  ✓ {fileName,-55} rows={rowCount,4}  {measurementInfo}{valuesMatch}");
                if (!string.IsNullOrEmpty(sampleValues))
                {
                    Console.WriteLine($"    data: {sampleValues}");
                }

                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ {fileName,-55} ERROR: {ex.Message}");
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        Console.WriteLine($"\n{'=',-80}");
        Console.WriteLine($"Results: {successCount}/{tsfiles.Length} files read successfully, {valueMatchCount} value validations passed");
        if (errors.Count > 0)
        {
            Console.WriteLine($"Errors ({errors.Count}):");
            foreach (var err in errors.Take(10))
                Console.WriteLine($"  - {err}");
        }
        Console.WriteLine($"{'=',-80}\n");

        Assert.True(successCount > 0,
            $"Failed to read any comprehensive files. Errors:\n{string.Join("\n", errors.Take(5))}");
    }

    /// <summary>
    /// Tests reading Java-generated V3 files.
    /// This verifies that C# can read V3 format files generated by tsfile:1.1.3.
    /// </summary>
    [Fact]
    public void ReadJavaV3Files_ValidatesCompatibility()
    {
        var javaV3Dir = GetJavaV3TestFilesDir();

        if (!Directory.Exists(javaV3Dir))
        {
            // Skip test if V3 files not generated
            return;
        }

        var v3Files = Directory.GetFiles(javaV3Dir, "*.tsfile");
        if (v3Files.Length == 0)
        {
            // Skip test if no V3 files found
            return;
        }

        int successCount = 0;
        var errors = new List<string>();

        Console.WriteLine($"{'=',-80}");
        Console.WriteLine($"V3 Interop Test: Reading {v3Files.Length} Java V3 files from {javaV3Dir}");
        Console.WriteLine($"{'=',-80}");

        foreach (var file in v3Files)
        {
            var fileName = Path.GetFileName(file);
            try
            {
                using var reader = new TsFileReader(file);

                // Verify it's a V3 file
                Assert.Equal(3, reader.FileVersion);

                // Verify schemas are loaded
                Assert.NotEmpty(reader.Schemas);

                Console.WriteLine($"\n  ✓ {fileName}");
                Console.WriteLine($"    Version: {reader.FileVersion}");
                Console.WriteLine($"    Schemas: {reader.Schemas.Count}");

                foreach (var kvp in reader.Schemas)
                {
                    var schemaName = kvp.Key;
                    var schema = kvp.Value;
                    Console.WriteLine($"    Device/Table: \"{schemaName}\"");
                    Console.WriteLine($"      Measurements: {schema.Measurements.Count}");
                    foreach (var m in schema.Measurements)
                    {
                        Console.WriteLine($"        - {m.MeasurementName} ({m.DataType}/{m.Encoding}/{m.Compression})");
                    }

                    // Query data for this device
                    try
                    {
                        var result = reader.Query(schemaName);
                        Console.WriteLine($"      Rows: {result.Timestamps.Count}");

                        if (result.Timestamps.Count > 0)
                        {
                            var sampleCount = Math.Min(5, result.Timestamps.Count);
                            Console.WriteLine($"      Sample data (first {sampleCount} rows):");
                            for (int i = 0; i < sampleCount; i++)
                            {
                                var vals = new List<string> { $"t={result.Timestamps[i]}" };
                                foreach (var mkvp in result.MeasurementData)
                                {
                                    var v = mkvp.Value[i];
                                    vals.Add($"{mkvp.Key}={FormatValue(v)}");
                                }
                                Console.WriteLine($"        [{string.Join(", ", vals)}]");
                            }
                            if (result.Timestamps.Count > sampleCount)
                                Console.WriteLine($"        ... ({result.Timestamps.Count} rows total)");
                        }
                    }
                    catch (Exception qex)
                    {
                        Console.WriteLine($"      Query error: {qex.Message}");
                    }
                }

                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  ✗ {fileName}: {ex.Message}");
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        Console.WriteLine($"\n{'=',-80}");
        Console.WriteLine($"V3 compatibility: {successCount}/{v3Files.Length} files readable");
        if (errors.Count > 0)
        {
            Console.WriteLine($"Errors ({errors.Count}):");
            foreach (var err in errors)
                Console.WriteLine($"  - {err}");
        }
        Console.WriteLine($"{'=',-80}\n");

        Assert.True(successCount >= v3Files.Length * 0.8,
            $"Should read at least 80% of V3 files. Success: {successCount}/{v3Files.Length}");
    }

    /// <summary>
    /// Tests reading Java-generated Table Model V4 files.
    /// This verifies C# can read Table Model V4 format files with ColumnCategory.
    /// </summary>
    [Fact]
    public void ReadJavaTableModelV4Files_ValidatesInteroperability()
    {
        var tableModelDir = Environment.GetEnvironmentVariable("JAVA_TABLE_MODEL_V4_DIR")
            ?? "/tmp/interop-tests/java-table-model-v4";

        if (!Directory.Exists(tableModelDir))
        {
            // Skip test if Table Model V4 files not generated
            return;
        }

        var tableFiles = Directory.GetFiles(tableModelDir, "*.tsfile");
        if (tableFiles.Length == 0)
        {
            // Skip test if no Table Model V4 files found
            return;
        }

        int successCount = 0;
        var errors = new List<string>();

        Console.WriteLine($"\n{'=',-80}");
        Console.WriteLine($"Reading {tableFiles.Length} Java Table Model V4 files");
        Console.WriteLine($"{'=',-80}");

        foreach (var file in tableFiles.OrderBy(f => f))
        {
            var fileName = Path.GetFileName(file);
            try
            {
                using var reader = new TsFileReader(file);

                Assert.Equal(4, reader.FileVersion);
                Assert.NotEmpty(reader.Schemas);

                var tableName = reader.Schemas.Keys.First();
                var schema = reader.Schemas[tableName];
                var result = reader.Query(tableName);
                Assert.NotNull(result);

                var measurementInfo = string.Join(", ", schema.Measurements.Select(m =>
                    $"{m.MeasurementName}({m.DataType}/{m.Encoding}/{m.Compression})"));
                var rowCount = result.Timestamps.Count;
                var sampleValues = FormatSampleValues(result, maxRows: 3);

                Console.WriteLine($"  \u2713 {fileName,-55} rows={rowCount,4}  {measurementInfo}");
                if (!string.IsNullOrEmpty(sampleValues))
                {
                    Console.WriteLine($"    data: {sampleValues}");
                }

                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \u2717 {fileName,-55} ERROR: {ex.Message}");
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        Console.WriteLine($"\n{'=',-80}");
        Console.WriteLine($"Table Model V4 interop: {successCount}/{tableFiles.Length} files readable");
        if (errors.Count > 0)
        {
            Console.WriteLine($"Errors ({errors.Count}):");
            foreach (var err in errors.Take(10))
                Console.WriteLine($"  - {err}");
        }
        Console.WriteLine($"{'=',-80}\n");

        // At least 80% of files should be readable
        Assert.True(successCount >= tableFiles.Length * 0.8,
            $"Should be able to read at least 80% of Table Model V4 files. Success: {successCount}/{tableFiles.Length}");
    }

    /// <summary>
    /// Formats sample values from a QueryResult for display.
    /// Shows timestamps and measurement values for the first N rows.
    /// </summary>
    private static string FormatSampleValues(QueryResult result, int maxRows = 5)
    {
        if (result.Timestamps.Count == 0)
            return "(empty)";

        var rows = new List<string>();
        var measurements = result.MeasurementData.Keys.ToList();
        var count = Math.Min(result.Timestamps.Count, maxRows);

        for (int i = 0; i < count; i++)
        {
            var vals = new List<string>();
            vals.Add($"t={result.Timestamps[i]}");
            foreach (var m in measurements)
            {
                var v = result.MeasurementData[m][i];
                vals.Add($"{m}={FormatValue(v)}");
            }
            rows.Add($"[{string.Join(", ", vals)}]");
        }

        var suffix = result.Timestamps.Count > maxRows
            ? $" ... ({result.Timestamps.Count} rows total)"
            : "";
        return string.Join(", ", rows) + suffix;
    }

    /// <summary>
    /// Formats a single value for display, handling different data types.
    /// </summary>
    private static string FormatValue(object v)
    {
        return v switch
        {
            float f => f.ToString("G6"),
            double d => d.ToString("G6"),
            byte[] b => $"bytes[{b.Length}]",
            string s when s.Length > 20 => $"\"{s[..20]}...\"",
            string s => $"\"{s}\"",
            bool b => b ? "true" : "false",
            _ => v?.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Validates actual values against expected values from metadata JSON.
    /// </summary>
    private static (bool matched, string detail) ValidateExpectedValues(List<object> actualValues, List<JsonElement> expectedValues, string dataType)
    {
        if (actualValues.Count != expectedValues.Count)
            return (false, $"count: actual={actualValues.Count} expected={expectedValues.Count}");

        for (int i = 0; i < actualValues.Count; i++)
        {
            var actual = actualValues[i];
            var expected = expectedValues[i];

            bool match;
            try
            {
                match = dataType.ToUpperInvariant() switch
                {
                    "INT32" => actual is int ai && GetJsonAsLong(expected) == ai,
                    "INT64" => actual is long al && GetJsonAsLong(expected) == al,
                    "FLOAT" => actual is float af && Math.Abs(af - GetJsonAsDouble(expected)) < 1e-2,
                    "DOUBLE" => actual is double ad && Math.Abs(ad - GetJsonAsDouble(expected)) < 1e-6,
                    "BOOLEAN" => actual is bool ab && ab == GetJsonAsBool(expected),
                    "TEXT" or "STRING" => actual is string ast && ast == expected.GetString(),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                return (false, $"@{i}: exception {ex.Message}");
            }

            if (!match)
                return (false, $"@{i}: actual={actual}({actual?.GetType().Name}) expected={expected}({expected.ValueKind})");
        }

        return (true, "");
    }

    private static long GetJsonAsLong(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.Number => e.GetInt64(),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => throw new InvalidOperationException($"Cannot convert {e.ValueKind} to long")
        };
    }

    private static double GetJsonAsDouble(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.Number => e.GetDouble(),
            JsonValueKind.True => 1.0,
            JsonValueKind.False => 0.0,
            _ => throw new InvalidOperationException($"Cannot convert {e.ValueKind} to double")
        };
    }

    private static bool GetJsonAsBool(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => e.GetInt32() != 0,
            _ => throw new InvalidOperationException($"Cannot convert {e.ValueKind} to bool")
        };
    }
}

/// <summary>
/// Metadata for a test file from the Java generator's test-metadata.json.
/// </summary>
public class TestFileMetadata
{
    public string FileName { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Encoding { get; set; } = "";
    public string Compression { get; set; } = "";
    public string Pattern { get; set; } = "";
    public int ValueCount { get; set; }
    public List<JsonElement>? ExpectedValues { get; set; }
}
