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

using Apache.TsFile;
using Apache.TsFile.Enums;
using Apache.TsFile.IO;
using Apache.TsFile.Schema;

namespace BasicExample;

class Program
{
    static int Main(string[] args)
    {
        Console.WriteLine("Apache TSFile C# Library - Basic Example\n");
        
        // Handle command line arguments for interop testing
        if (args.Length >= 2)
        {
            var command = args[0];
            var filePath = args[1];
            
            try
            {
                if (command.Equals("read", StringComparison.OrdinalIgnoreCase))
                {
                    return ReadFileExample(filePath) ? 0 : 1;
                }
                else if (command.Equals("write", StringComparison.OrdinalIgnoreCase))
                {
                    WriteExample(filePath);
                    Console.WriteLine($"Successfully wrote to {filePath}");
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Unknown command: {command}. Use 'read' or 'write'.");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
        
        var defaultFilePath = "example.tsfile";
        
        // Example 1: Write data to TSFile
        Console.WriteLine("Writing data to TSFile...");
        WriteExample(defaultFilePath);
        
        // Example 2: Read data from TSFile
        Console.WriteLine("\nReading data from TSFile...");
        ReadExample(defaultFilePath);
        
        // Example 3: Write with compression
        Console.WriteLine("\nWriting compressed data...");
        WriteCompressedExample("compressed.tsfile");
        
        Console.WriteLine("\nExamples completed successfully!");
        
        // Cleanup
        File.Delete(defaultFilePath);
        File.Delete("compressed.tsfile");
        
        return 0;
    }
    
    /// <summary>
    /// Reads a TsFile and prints its contents. Returns true on success.
    /// </summary>
    static bool ReadFileExample(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"File not found: {filePath}");
            return false;
        }
        
        Console.WriteLine($"Reading file: {filePath}");
        
        // Detect file version and use appropriate reader
        var version = DetectTsFileVersion(filePath);
        Console.WriteLine($"Detected TSFile version: {version}");
        
        if (version == 4)
        {
            return ReadFileV4Example(filePath);
        }
        else
        {
            return ReadFileV3Example(filePath);
        }
    }
    
    /// <summary>
    /// Detects the version of a TsFile by reading its header.
    /// </summary>
    static byte DetectTsFileVersion(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        
        // Read magic string (6 bytes)
        var magic = reader.ReadBytes(6);
        var expectedMagic = new byte[] { 0x54, 0x73, 0x46, 0x69, 0x6C, 0x65 }; // "TsFile"
        
        if (!magic.SequenceEqual(expectedMagic))
            throw new InvalidDataException("Invalid TSFile magic string");
        
        // Read version byte
        return reader.ReadByte();
    }
    
    /// <summary>
    /// Reads a V4 format TsFile using TsFileReaderV4.
    /// </summary>
    static bool ReadFileV4Example(string filePath)
    {
        using var reader = new TsFileReaderV4(filePath);
        
        // Print available schemas
        Console.WriteLine($"Found {reader.Schemas.Count} table(s):");
        foreach (var schema in reader.Schemas.Values)
        {
            Console.WriteLine($"  - {schema.TableName} with {schema.ColumnSchemas?.Count ?? 0} column(s)");
            if (schema.ColumnSchemas != null)
            {
                foreach (var column in schema.ColumnSchemas)
                {
                    Console.WriteLine($"      - {column.Name}: {column.DataType} ({column.Category})");
                }
            }
        }
        
        // Query and print data for each table
        foreach (var tableName in reader.Schemas.Keys)
        {
            var result = reader.Query(tableName);
            var totalRows = result.DeviceTimestamps.Values.Sum(ts => ts.Count);
            Console.WriteLine($"\nTable '{tableName}': {totalRows} total rows across {result.DeviceTimestamps.Count} device(s)");
            
            foreach (var deviceId in result.DeviceTimestamps.Keys)
            {
                var timestamps = result.DeviceTimestamps[deviceId];
                if (timestamps.Count > 0)
                {
                    Console.WriteLine($"  Device '{deviceId}': {timestamps.Count} rows");
                    Console.WriteLine($"    First timestamp: {timestamps[0]}");
                    Console.WriteLine($"    Last timestamp: {timestamps[^1]}");
                    if (result.DeviceData.ContainsKey(deviceId))
                    {
                        Console.WriteLine($"    Columns: {string.Join(", ", result.DeviceData[deviceId].Keys)}");
                    }
                }
            }
        }
        
        Console.WriteLine("\nFile read successfully!");
        return true;
    }
    
    /// <summary>
    /// Reads a V3 format TsFile using TsFileReader.
    /// </summary>
    static bool ReadFileV3Example(string filePath)
    {
        using var reader = new TsFileReader(filePath);
        
        // Print available schemas
        Console.WriteLine($"Found {reader.Schemas.Count} device(s):");
        foreach (var schema in reader.Schemas.Values)
        {
            Console.WriteLine($"  - {schema.TableName} with {schema.MeasurementCount} measurement(s)");
            foreach (var measurement in schema.Measurements)
            {
                Console.WriteLine($"      - {measurement.MeasurementName}: {measurement.DataType}");
            }
        }
        
        // Query and print data for each device
        foreach (var deviceName in reader.Schemas.Keys)
        {
            var result = reader.Query(deviceName);
            Console.WriteLine($"\nDevice '{deviceName}': {result.Timestamps.Count} rows");
            
            if (result.Timestamps.Count > 0)
            {
                Console.WriteLine($"  Measurements: {string.Join(", ", result.MeasurementData.Keys)}");
                Console.WriteLine($"  First timestamp: {result.Timestamps[0]}");
                Console.WriteLine($"  Last timestamp: {result.Timestamps[^1]}");
            }
        }
        
        Console.WriteLine("\nFile read successfully!");
        return true;
    }
    
    static void WriteExample(string filePath)
    {
        using var writer = new TsFileWriter(filePath);
        
        // Define measurement schemas
        var measurements = new List<MeasurementSchema>
        {
            new MeasurementSchema("temperature", TsDataType.Float, TsEncoding.Plain, CompressionType.Uncompressed),
            new MeasurementSchema("humidity", TsDataType.Int32, TsEncoding.Plain, CompressionType.Uncompressed),
            new MeasurementSchema("status", TsDataType.Boolean, TsEncoding.Plain, CompressionType.Uncompressed)
        };
        
        // Register device
        writer.RegisterDevice("sensor_1", measurements);
        
        // Create a tablet for batch writing
        var tablet = new Tablet("sensor_1", measurements, 100);
        
        // Add data rows
        for (int i = 0; i < 10; i++)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i * 1000;
            float temperature = 20.0f + i * 0.5f;
            int humidity = 50 + i;
            bool status = i % 2 == 0;
            
            tablet.AddRow(timestamp, temperature, humidity, status);
        }
        
        // Write tablet to file
        writer.Write(tablet);
        
        // Alternative: Write single rows
        writer.WriteRow("sensor_1", 
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 10000, 
            25.5f, 60, true);
        
        writer.Close();
        
        Console.WriteLine($"Written 11 rows to {filePath}");
    }
    
    static void ReadExample(string filePath)
    {
        using var reader = new TsFileReader(filePath);
        
        // Print available schemas
        Console.WriteLine($"Found {reader.Schemas.Count} device(s):");
        foreach (var schema in reader.Schemas.Values)
        {
            Console.WriteLine($"  - {schema.TableName} with {schema.MeasurementCount} measurement(s)");
        }
        
        // Query all data for a device
        var result = reader.Query("sensor_1");
        
        Console.WriteLine($"\nRead {result.Timestamps.Count} rows:");
        
        for (int i = 0; i < Math.Min(5, result.Timestamps.Count); i++)
        {
            Console.WriteLine($"  Row {i}: " +
                $"timestamp={result.Timestamps[i]}, " +
                $"temperature={result.MeasurementData["temperature"][i]}, " +
                $"humidity={result.MeasurementData["humidity"][i]}, " +
                $"status={result.MeasurementData["status"][i]}");
        }
        
        if (result.Timestamps.Count > 5)
        {
            Console.WriteLine($"  ... and {result.Timestamps.Count - 5} more rows");
        }
    }
    
    static void WriteCompressedExample(string filePath)
    {
        using var writer = new TsFileWriter(filePath);
        
        // Use compression for better storage efficiency
        var measurements = new List<MeasurementSchema>
        {
            new MeasurementSchema("temperature", TsDataType.Double, TsEncoding.Plain, CompressionType.Gzip),
            new MeasurementSchema("pressure", TsDataType.Double, TsEncoding.Plain, CompressionType.Lz4)
        };
        
        writer.RegisterDevice("weather_station", measurements);
        
        var tablet = new Tablet("weather_station", measurements, 1000);
        
        // Generate sample data
        for (int i = 0; i < 100; i++)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i * 60000;
            double temperature = 15.0 + Math.Sin(i * 0.1) * 10.0;
            double pressure = 1013.25 + Math.Cos(i * 0.1) * 20.0;
            
            tablet.AddRow(timestamp, temperature, pressure);
        }
        
        writer.Write(tablet);
        writer.Close();
        
        var fileSize = new FileInfo(filePath).Length;
        Console.WriteLine($"Written 100 rows to {filePath} (size: {fileSize} bytes)");
    }
}
