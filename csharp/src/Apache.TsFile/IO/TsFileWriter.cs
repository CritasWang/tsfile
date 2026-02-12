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

using Apache.TsFile.Common;
using Apache.TsFile.Compress;
using Apache.TsFile.Encoding;
using Apache.TsFile.Enums;
using Apache.TsFile.Schema;

namespace Apache.TsFile.IO;

/// <summary>
/// Writer for creating TSFile format files.
/// Supports both V3 and V4 formats, with V4 as the default.
/// </summary>
public class TsFileWriter : IDisposable
{
    private readonly string _filePath;
    private readonly FileStream _fileStream;
    private readonly BinaryWriter _writer;
    private readonly Dictionary<string, TableSchema> _schemas;
    private readonly Dictionary<string, MemoryStream> _deviceChunkBuffers;
    private readonly List<ChunkGroupInfo> _chunkGroups;
    private bool _disposed;
    private bool _closed;
    private bool _headerWritten;
    private long _dataStartPosition;

    /// <summary>
    /// Gets the file version being written (3 or 4).
    /// </summary>
    public byte FileVersion { get; }

    /// <summary>
    /// Initializes a new instance of the TsFileWriter class with default V4 format.
    /// </summary>
    public TsFileWriter(string filePath) : this(filePath, TsFileConstants.DefaultFileVersion)
    {
    }

    /// <summary>
    /// Initializes a new instance of the TsFileWriter class with specified version.
    /// </summary>
    public TsFileWriter(string filePath, byte fileVersion)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        if (fileVersion != 3 && fileVersion != 4)
            throw new ArgumentException("File version must be 3 or 4", nameof(fileVersion));

        _filePath = filePath;
        FileVersion = fileVersion;
        _schemas = new Dictionary<string, TableSchema>();
        _deviceChunkBuffers = new Dictionary<string, MemoryStream>();
        _chunkGroups = new List<ChunkGroupInfo>();

        _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        _writer = new BinaryWriter(_fileStream);

        WriteHeader();
    }
    
    /// <summary>
    /// Registers a table schema for writing.
    /// </summary>
    public void RegisterTableSchema(TableSchema schema)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        if (_schemas.ContainsKey(schema.TableName))
            throw new ArgumentException($"Schema for table {schema.TableName} already registered");

        _schemas[schema.TableName] = schema;
        if (FileVersion != 4)
        {
            _deviceChunkBuffers[schema.TableName] = new MemoryStream();
        }
    }

    /// <summary>
    /// Registers a table schema for writing (alias for RegisterTableSchema).
    /// </summary>
    public void RegisterTable(TableSchema schema) => RegisterTableSchema(schema);
    
    /// <summary>
    /// Registers a device with measurement schemas.
    /// </summary>
    public void RegisterDevice(string deviceName, List<MeasurementSchema> measurements)
    {
        // Parse device name to get the table name for consistent V4 metadata
        var deviceId = new StringArrayDeviceID(deviceName);
        var tableName = deviceId.GetTableName().ToLowerInvariant();

        // Multiple devices may map to the same table; skip if already registered
        if (_schemas.ContainsKey(tableName))
            return;

        var schema = new TableSchema(tableName);
        foreach (var measurement in measurements)
        {
            schema.AddMeasurement(measurement);
        }
        RegisterTableSchema(schema);
    }

    /// <summary>
    /// Registers a timeseries using tree model path (e.g., "root.db1.d1.s1").
    /// Automatically converts to table model for V4 format.
    /// </summary>
    public void RegisterTimeseries(string devicePath, List<MeasurementSchema> measurements)
    {
        var schema = TableSchema.CreateFromTreeModel(devicePath, measurements);
        RegisterTableSchema(schema);
    }
    
    /// <summary>
    /// Writes a tablet of data to the file.
    /// </summary>
    public void Write(Tablet tablet)
    {
        if (tablet == null)
            throw new ArgumentNullException(nameof(tablet));

        // Parse device name to get table name for schema lookup
        var deviceId = new StringArrayDeviceID(tablet.DeviceName);
        var tableName = deviceId.GetTableName().ToLowerInvariant();

        if (!_schemas.TryGetValue(tableName, out var schema))
            throw new ArgumentException($"Schema for device {tablet.DeviceName} (table: {tableName}) not registered");

        if (tablet.RowCount == 0)
            return;

        // Set column categories on tablet from schema (for device splitting)
        if (tablet.ColumnCategories == null && schema.ColumnCategories.Count > 0)
            tablet.SetColumnCategories(schema.ColumnCategories);

        if (FileVersion == 4)
        {
            // V4: for table model with TAG columns, split by device
            bool hasTagColumns = schema.GetTagColumnCount() > 0;
            if (hasTagColumns)
            {
                var deviceSplits = SplitTabletByDevice(tablet);
                foreach (var (splitDeviceId, startRow, endRow) in deviceSplits)
                {
                    WriteChunkDataV4(tablet, schema, splitDeviceId, startRow, endRow);
                }
            }
            else
            {
                WriteChunkDataV4(tablet, schema, deviceId, 0, tablet.RowCount);
            }
        }
        else
        {
            // V3 writes to buffer first
            var buffer = _deviceChunkBuffers[tableName];
            WriteChunkDataV3(buffer, tablet, schema);

            if (buffer.Length >= TsFileConstants.DefaultChunkSize)
            {
                FlushDeviceBuffer(tableName);
            }
        }
    }

    /// <summary>
    /// Writes a tablet of data to the file (alias for Write).
    /// </summary>
    public void WriteTable(Tablet tablet) => Write(tablet);
    
    /// <summary>
    /// Writes a single row of data.
    /// </summary>
    public void WriteRow(string deviceName, long timestamp, params object[] values)
    {
        // Parse device name to get table name for schema lookup
        var deviceId = new StringArrayDeviceID(deviceName);
        var tableName = deviceId.GetTableName().ToLowerInvariant();

        if (!_schemas.TryGetValue(tableName, out var schema))
            throw new ArgumentException($"Schema for device {deviceName} (table: {tableName}) not registered");

        var tablet = new Tablet(deviceName, schema.Measurements, 1);
        tablet.AddRow(timestamp, values);
        Write(tablet);
    }
    
    /// <summary>
    /// Flushes all buffered data and closes the file.
    /// </summary>
    public void Close()
    {
        if (_closed || _disposed)
            return;
        _closed = true;

        // Flush all device buffers
        foreach (var deviceName in _schemas.Keys.ToList())
        {
            FlushDeviceBuffer(deviceName);
        }

        // Write separator and metadata based on version
        if (FileVersion == 4)
        {
            WriteV4Metadata();
        }
        else
        {
            WriteV3Metadata();
        }

        // Write footer
        WriteFooter();

        // Ensure all data is written to disk and close streams
        _writer.Flush();
        _fileStream.Flush();
        _writer.Dispose();
        _fileStream.Dispose();

        // Clean up device buffers
        foreach (var buffer in _deviceChunkBuffers.Values)
        {
            buffer?.Dispose();
        }

        // Mark as disposed to prevent double-dispose
        _disposed = true;
        GC.SuppressFinalize(this);
    }
    
    private void WriteHeader()
    {
        if (_headerWritten)
            return;

        // Write magic string
        _writer.Write(TsFileConstants.MagicString);

        // Write version (V3 or V4)
        _writer.Write(FileVersion);

        _dataStartPosition = _fileStream.Position;
        _headerWritten = true;
    }
    
    private void WriteChunkDataV3(MemoryStream buffer, Tablet tablet, TableSchema schema)
    {
        using var chunkWriter = new BinaryWriter(buffer, System.Text.Encoding.UTF8, true);
        
        chunkWriter.Write(TsFileConstants.ChunkHeaderMarker);
        chunkWriter.Write(tablet.DeviceName);
        
        for (int i = 0; i < schema.Measurements.Count; i++)
        {
            var measurement = schema.Measurements[i];
            var encoder = EncoderFactory.CreateEncoder(measurement.Encoding, measurement.DataType);
            var compressor = CompressorFactory.GetCompressor(measurement.Compression);
            
            using var valueStream = new MemoryStream();
            EncodeColumn(encoder, tablet, i, valueStream);
            
            var encodedData = valueStream.ToArray();
            var compressedData = compressor.Compress(encodedData);
            
            chunkWriter.Write(measurement.MeasurementName);
            chunkWriter.Write((byte)measurement.DataType);
            chunkWriter.Write((byte)measurement.Encoding);
            chunkWriter.Write((byte)measurement.Compression);
            chunkWriter.Write(compressedData.Length);
            chunkWriter.Write(encodedData.Length);
            chunkWriter.Write(compressedData);
        }
        
        using var timestampStream = new MemoryStream();
        for (int i = 0; i < tablet.RowCount; i++)
        {
            WriteInt64BigEndian(timestampStream, tablet.Timestamps[i]);
        }
        
        var timestampData = timestampStream.ToArray();
        chunkWriter.Write(timestampData.Length);
        chunkWriter.Write(timestampData);
        chunkWriter.Write(tablet.RowCount);
    }

    /// <summary>
    /// Splits a tablet by device ID based on TAG column values.
    /// Returns (deviceId, startRow, endRow) tuples.
    /// </summary>
    private static List<(IDeviceID DeviceId, int StartRow, int EndRow)> SplitTabletByDevice(Tablet tablet)
    {
        var result = new List<(IDeviceID, int, int)>();
        IDeviceID? lastDeviceId = null;
        int lastStart = 0;

        for (int i = 0; i < tablet.RowCount; i++)
        {
            var currDeviceId = tablet.GetDeviceID(i);
            if (lastDeviceId != null && !currDeviceId.Equals(lastDeviceId))
            {
                result.Add((lastDeviceId, lastStart, i));
                lastStart = i;
            }
            lastDeviceId = currDeviceId;
        }
        if (lastDeviceId != null)
            result.Add((lastDeviceId, lastStart, tablet.RowCount));

        return result;
    }

    private void WriteChunkDataV4(Tablet tablet, TableSchema schema, IDeviceID deviceId, int startRow, int endRow)
    {
        bool isTreeModel = schema.GetTagColumnCount() == 0 && (schema.ColumnSchemas == null || schema.ColumnSchemas.Count == 0);

        // Write ChunkGroupHeader: [marker:0x00][deviceID]
        var chunkGroupStart = _fileStream.Position;
        _writer.Write((byte)0x00);
        deviceId.Serialize(_writer);

        var chunkGroupInfo = new ChunkGroupInfo
        {
            DeviceId = deviceId,
            StartOffset = chunkGroupStart
        };

        if (isTreeModel)
        {
            WriteTreeModelChunksV4(tablet, schema, chunkGroupInfo, startRow, endRow);
        }
        else
        {
            WriteTableModelChunksV4(tablet, schema, chunkGroupInfo, startRow, endRow);
        }

        chunkGroupInfo.EndOffset = _fileStream.Position;
        _chunkGroups.Add(chunkGroupInfo);
    }

    private void WriteTreeModelChunksV4(Tablet tablet, TableSchema schema, ChunkGroupInfo chunkGroupInfo, int startRow, int endRow)
    {
        int rowCount = endRow - startRow;
        // Tree model: each measurement is a non-aligned chunk with interleaved time+value pages
        // Encode timestamps once (shared across all measurements)
        var timeEncoder = EncoderFactory.CreateEncoder(TsEncoding.Ts2Diff, TsDataType.Int64);
        using var timeStream = new MemoryStream();
        for (int row = startRow; row < endRow; row++)
            timeEncoder.Encode(tablet.Timestamps[row], timeStream);
        timeEncoder.Flush(timeStream);
        var timeBuffer = timeStream.ToArray();

        long startTime = tablet.Timestamps[startRow];
        long endTime = tablet.Timestamps[endRow - 1];

        for (int i = 0; i < schema.Measurements.Count; i++)
        {
            var measurement = schema.Measurements[i];
            var compressor = CompressorFactory.GetCompressor(measurement.Compression);

            // Encode values
            var valueEncoder = EncoderFactory.CreateEncoder(measurement.Encoding, measurement.DataType);
            using var valueStream = new MemoryStream();
            EncodeColumnRange(valueEncoder, tablet, i, startRow, endRow, valueStream);
            var valueBuffer = valueStream.ToArray();

            // Build page data: [timeBufferLength:unsignedVarInt][timeBuffer][valueBuffer]
            using var pageDataStream = new MemoryStream();
            WriteUnsignedVarIntToStream(pageDataStream, timeBuffer.Length);
            pageDataStream.Write(timeBuffer, 0, timeBuffer.Length);
            pageDataStream.Write(valueBuffer, 0, valueBuffer.Length);
            var pageData = pageDataStream.ToArray();

            // Compress page data
            var compressedPageData = CompressPageData(compressor, pageData, measurement.Compression);
            int uncompressedSize = pageData.Length;
            int compressedSize = compressedPageData.Length;

            // Build page header: [uncompressedSize:unsignedVarInt][compressedSize:unsignedVarInt]
            using var pageHeaderStream = new MemoryStream();
            WriteUnsignedVarIntToStream(pageHeaderStream, uncompressedSize);
            WriteUnsignedVarIntToStream(pageHeaderStream, compressedSize);
            var pageHeader = pageHeaderStream.ToArray();

            int dataSize = pageHeader.Length + compressedSize;

            // Write chunk header: [marker:0x05][measurementId][dataSize][dataType][compression][encoding]
            var chunkOffset = _fileStream.Position;
            _writer.Write((byte)0x05); // ONLY_ONE_PAGE_CHUNK_HEADER
            WriteVarIntString(measurement.MeasurementName);
            WriteUnsignedVarInt(dataSize);
            _writer.Write((byte)measurement.DataType);
            _writer.Write((byte)measurement.Compression);
            _writer.Write((byte)measurement.Encoding);

            // Write page header + compressed page data
            _writer.Write(pageHeader);
            _writer.Write(compressedPageData);

            var stats = ComputeColumnStatisticsRange(tablet, i, measurement.DataType, startRow, endRow);
            chunkGroupInfo.Chunks.Add(new ChunkInfo
            {
                MeasurementId = measurement.MeasurementName,
                DataType = measurement.DataType,
                Encoding = measurement.Encoding,
                Compression = measurement.Compression,
                Offset = chunkOffset,
                IsTimeChunk = false,
                Count = rowCount,
                StartTime = startTime,
                EndTime = endTime,
                MinValue = stats.Min,
                MaxValue = stats.Max,
                FirstValue = stats.First,
                LastValue = stats.Last,
                SumValue = stats.Sum
            });
        }
    }

    private void WriteTableModelChunksV4(Tablet tablet, TableSchema schema, ChunkGroupInfo chunkGroupInfo, int startRow, int endRow)
    {
        int rowCount = endRow - startRow;
        // Table model: separate time chunk + value chunks (aligned)
        // Only FIELD columns are written as value chunks; TAG columns form the device ID
        long startTime = tablet.Timestamps[startRow];
        long endTime = tablet.Timestamps[endRow - 1];

        // Write time chunk
        var timeCompression = CompressionType.Uncompressed;
        var timeCompressor = CompressorFactory.GetCompressor(timeCompression);
        var timeEncoder = EncoderFactory.CreateEncoder(TsEncoding.Ts2Diff, TsDataType.Int64);
        using var timeStream = new MemoryStream();
        for (int row = startRow; row < endRow; row++)
            timeEncoder.Encode(tablet.Timestamps[row], timeStream);
        timeEncoder.Flush(timeStream);
        var timeData = timeStream.ToArray();
        var compressedTimeData = CompressPageData(timeCompressor, timeData, timeCompression);

        using var timePageHeader = new MemoryStream();
        WriteUnsignedVarIntToStream(timePageHeader, timeData.Length);
        WriteUnsignedVarIntToStream(timePageHeader, compressedTimeData.Length);
        var timePageHeaderBytes = timePageHeader.ToArray();
        int timeDataSize = timePageHeaderBytes.Length + compressedTimeData.Length;

        var timeChunkOffset = _fileStream.Position;
        _writer.Write((byte)0x85); // ONLY_ONE_PAGE_TIME_CHUNK_HEADER
        WriteVarIntString("");
        WriteUnsignedVarInt(timeDataSize);
        _writer.Write((byte)TsDataType.Int64);
        _writer.Write((byte)timeCompression);
        _writer.Write((byte)TsEncoding.Ts2Diff);
        _writer.Write(timePageHeaderBytes);
        _writer.Write(compressedTimeData);

        chunkGroupInfo.Chunks.Add(new ChunkInfo
        {
            MeasurementId = "",
            DataType = TsDataType.Int64,
            Encoding = TsEncoding.Ts2Diff,
            Compression = timeCompression,
            Offset = timeChunkOffset,
            IsTimeChunk = true,
            Count = rowCount,
            StartTime = startTime,
            EndTime = endTime
        });

        // Write value chunks — only FIELD columns
        for (int i = 0; i < schema.Measurements.Count; i++)
        {
            // Skip TAG columns — they are part of the device ID, not data chunks
            if (i < schema.ColumnCategories.Count && schema.ColumnCategories[i] == ColumnCategory.Tag)
                continue;

            var measurement = schema.Measurements[i];
            var compressor = CompressorFactory.GetCompressor(measurement.Compression);
            var encoder = EncoderFactory.CreateEncoder(measurement.Encoding, measurement.DataType);

            using var valueStream = new MemoryStream();
            EncodeColumnRange(encoder, tablet, i, startRow, endRow, valueStream);
            var valueData = valueStream.ToArray();
            var compressedValueData = CompressPageData(compressor, valueData, measurement.Compression);

            using var valuePageHeader = new MemoryStream();
            WriteUnsignedVarIntToStream(valuePageHeader, valueData.Length);
            WriteUnsignedVarIntToStream(valuePageHeader, compressedValueData.Length);
            var valuePageHeaderBytes = valuePageHeader.ToArray();
            int valueDataSize = valuePageHeaderBytes.Length + compressedValueData.Length;

            var valueChunkOffset = _fileStream.Position;
            _writer.Write((byte)0x45); // ONLY_ONE_PAGE_VALUE_CHUNK_HEADER
            WriteVarIntString(measurement.MeasurementName);
            WriteUnsignedVarInt(valueDataSize);
            _writer.Write((byte)measurement.DataType);
            _writer.Write((byte)measurement.Compression);
            _writer.Write((byte)measurement.Encoding);
            _writer.Write(valuePageHeaderBytes);
            _writer.Write(compressedValueData);

            var valStats = ComputeColumnStatisticsRange(tablet, i, measurement.DataType, startRow, endRow);
            chunkGroupInfo.Chunks.Add(new ChunkInfo
            {
                MeasurementId = measurement.MeasurementName,
                DataType = measurement.DataType,
                Encoding = measurement.Encoding,
                Compression = measurement.Compression,
                Offset = valueChunkOffset,
                IsTimeChunk = false,
                Count = rowCount,
                StartTime = startTime,
                EndTime = endTime,
                MinValue = valStats.Min,
                MaxValue = valStats.Max,
                FirstValue = valStats.First,
                LastValue = valStats.Last,
                SumValue = valStats.Sum
            });
        }
    }

    private record ColumnStats(object? Min, object? Max, object? First, object? Last, double Sum);

    private static ColumnStats ComputeColumnStatistics(Tablet tablet, int columnIndex, TsDataType dataType)
    {
        return ComputeColumnStatisticsRange(tablet, columnIndex, dataType, 0, tablet.RowCount);
    }

    private static ColumnStats ComputeColumnStatisticsRange(Tablet tablet, int columnIndex, TsDataType dataType, int startRow, int endRow)
    {
        if (endRow <= startRow)
            return new ColumnStats(null, null, null, null, 0.0);

        var values = tablet.Values[columnIndex];
        switch (dataType)
        {
            case TsDataType.Boolean:
            {
                var arr = (bool[])values!;
                bool first = arr[startRow], last = arr[endRow - 1];
                long sum = 0;
                for (int r = startRow; r < endRow; r++) if (arr[r]) sum++;
                return new ColumnStats(null, null, first, last, sum);
            }
            case TsDataType.Int32:
            case TsDataType.Date:
            {
                var arr = (int[])values!;
                int min = arr[startRow], max = arr[startRow];
                double sum = 0;
                for (int r = startRow; r < endRow; r++)
                {
                    if (arr[r] < min) min = arr[r];
                    if (arr[r] > max) max = arr[r];
                    sum += arr[r];
                }
                return new ColumnStats(min, max, arr[startRow], arr[endRow - 1], sum);
            }
            case TsDataType.Int64:
            case TsDataType.Timestamp:
            {
                var arr = (long[])values!;
                long min = arr[startRow], max = arr[startRow];
                double sum = 0;
                for (int r = startRow; r < endRow; r++)
                {
                    if (arr[r] < min) min = arr[r];
                    if (arr[r] > max) max = arr[r];
                    sum += arr[r];
                }
                return new ColumnStats(min, max, arr[startRow], arr[endRow - 1], sum);
            }
            case TsDataType.Float:
            {
                var arr = (float[])values!;
                float min = arr[startRow], max = arr[startRow];
                double sum = 0;
                for (int r = startRow; r < endRow; r++)
                {
                    if (arr[r] < min) min = arr[r];
                    if (arr[r] > max) max = arr[r];
                    sum += arr[r];
                }
                return new ColumnStats(min, max, arr[startRow], arr[endRow - 1], sum);
            }
            case TsDataType.Double:
            {
                var arr = (double[])values!;
                double min = arr[startRow], max = arr[startRow], sum = 0;
                for (int r = startRow; r < endRow; r++)
                {
                    if (arr[r] < min) min = arr[r];
                    if (arr[r] > max) max = arr[r];
                    sum += arr[r];
                }
                return new ColumnStats(min, max, arr[startRow], arr[endRow - 1], sum);
            }
            case TsDataType.Text:
            case TsDataType.String:
            {
                var arr = (string[])values!;
                var first = arr[startRow] ?? string.Empty;
                var last = arr[endRow - 1] ?? string.Empty;
                return new ColumnStats(null, null, first, last, 0.0);
            }
            default:
                return new ColumnStats(null, null, null, null, 0.0);
        }
    }

    private static byte[] CompressPageData(ICompressor compressor, byte[] data, CompressionType compression)
    {
        if (compression == CompressionType.Uncompressed)
            return data;
        // For LZ4: Java format does NOT prepend 4-byte size, use raw LZ4 block
        if (compression == CompressionType.Lz4)
        {
            var maxSize = K4os.Compression.LZ4.LZ4Codec.MaximumOutputSize(data.Length);
            var output = new byte[maxSize];
            var compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(data, 0, data.Length, output, 0, maxSize);
            var result = new byte[compressedSize];
            Array.Copy(output, 0, result, 0, compressedSize);
            return result;
        }
        return compressor.Compress(data);
    }

    private static void WriteUnsignedVarIntToStream(Stream stream, int value)
    {
        while ((value & ~0x7F) != 0)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value = (int)((uint)value >> 7);
        }
        stream.WriteByte((byte)value);
    }

    private static void WriteUnsignedVarLongToStream(Stream stream, long value)
    {
        while ((value & ~0x7FL) != 0)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value = (long)((ulong)value >> 7);
        }
        stream.WriteByte((byte)value);
    }
    
    private void EncodeColumn(IEncoder encoder, Tablet tablet, int columnIndex, MemoryStream stream)
    {
        EncodeColumnRange(encoder, tablet, columnIndex, 0, tablet.RowCount, stream);
    }

    private void EncodeColumnRange(IEncoder encoder, Tablet tablet, int columnIndex, int startRow, int endRow, MemoryStream stream)
    {
        var schema = tablet.Schemas[columnIndex];
        var values = tablet.Values[columnIndex];
        
        for (int row = startRow; row < endRow; row++)
        {
            switch (schema.DataType)
            {
                case TsDataType.Boolean:
                    encoder.Encode(((bool[])values!)[row], stream);
                    break;
                case TsDataType.Int32:
                    encoder.Encode(((int[])values!)[row], stream);
                    break;
                case TsDataType.Int64:
                case TsDataType.Timestamp:
                    encoder.Encode(((long[])values!)[row], stream);
                    break;
                case TsDataType.Float:
                    encoder.Encode(((float[])values!)[row], stream);
                    break;
                case TsDataType.Double:
                    encoder.Encode(((double[])values!)[row], stream);
                    break;
                case TsDataType.Text:
                case TsDataType.String:
                    encoder.Encode(((string[])values!)[row], stream);
                    break;
            }
        }
        
        encoder.Flush(stream);
    }
    
    private void FlushDeviceBuffer(string deviceName)
    {
        if (!_deviceChunkBuffers.TryGetValue(deviceName, out var buffer))
            return;
        
        if (buffer.Length == 0)
            return;
        
        // Write buffer to file
        buffer.Position = 0;
        buffer.CopyTo(_fileStream);
        
        // Clear buffer
        buffer.SetLength(0);
        buffer.Position = 0;
    }
    
    private void WriteMetadata()
    {
        // Write metadata section
        var metadataStart = _fileStream.Position;

        // Write schema count
        _writer.Write(_schemas.Count);

        // Write each schema
        foreach (var schema in _schemas.Values)
        {
            schema.Serialize(_writer);
        }

        // Write metadata offset at the end
        var metadataEnd = _fileStream.Position;
        _writer.Write(metadataStart);
    }

    private void WriteV3Metadata()
    {
        WriteMetadata();
    }

    private void WriteV4Metadata()
    {
        // Write separator between data and metadata
        _writer.Write((byte)0x02);
        var metadataStartPos = _fileStream.Position;

        // Group chunks by device, then by measurement
        var deviceChunks = new Dictionary<string, Dictionary<string, List<ChunkInfo>>>();
        foreach (var cg in _chunkGroups)
        {
            var deviceName = cg.DeviceId.GetTableName();
            if (!deviceChunks.ContainsKey(deviceName))
                deviceChunks[deviceName] = new Dictionary<string, List<ChunkInfo>>();
            foreach (var chunk in cg.Chunks)
            {
                if (!deviceChunks[deviceName].ContainsKey(chunk.MeasurementId))
                    deviceChunks[deviceName][chunk.MeasurementId] = new List<ChunkInfo>();
                deviceChunks[deviceName][chunk.MeasurementId].Add(chunk);
            }
        }

        // Serialize TimeseriesMetadata for each measurement and build index
        var deviceMeasurementNodes = new Dictionary<string, MetadataIndexNode>();
        foreach (var (deviceName, measurements) in deviceChunks)
        {
            var measurementNode = new MetadataIndexNode(MetadataIndexNodeType.LeafMeasurement);
            foreach (var (measurementId, chunks) in measurements)
            {
                // Record position for index entry
                var tsMetadataOffset = _fileStream.Position;
                measurementNode.AddEntry(new MeasurementMetadataIndexEntry(measurementId, tsMetadataOffset));

                // Serialize TimeseriesMetadata
                WriteTimeseriesMetadata(measurementId, chunks);
            }
            measurementNode.SetEndOffset(_fileStream.Position);
            deviceMeasurementNodes[deviceName] = measurementNode;
        }

        // Build table index nodes
        // For tree model: table name = device path, node type = LeafMeasurement
        // For table model: table name from schema, node type = LeafDevice
        var tableIndexNodes = new Dictionary<string, MetadataIndexNode>();
        bool hasTableModel = _schemas.Values.Any(s => s.ColumnSchemas != null && s.ColumnSchemas.Count > 0);

        if (hasTableModel)
        {
            // Table model: group devices by table name
            var tableDevices = new Dictionary<string, List<(IDeviceID DeviceId, MetadataIndexNode Node)>>();
            foreach (var (deviceName, node) in deviceMeasurementNodes)
            {
                var tableName = deviceName;
                if (_schemas.TryGetValue(deviceName, out var schema))
                    tableName = schema.TableName;
                if (!tableDevices.ContainsKey(tableName))
                    tableDevices[tableName] = new List<(IDeviceID, MetadataIndexNode)>();
                tableDevices[tableName].Add((new StringArrayDeviceID(deviceName), node));
            }
            foreach (var (tableName, devices) in tableDevices)
            {
                var deviceNode = new MetadataIndexNode(MetadataIndexNodeType.LeafDevice);
                foreach (var (deviceId, node) in devices)
                {
                    // Serialize measurement node and record offset
                    var nodeOffset = _fileStream.Position;
                    node.Serialize(_writer);
                    deviceNode.AddEntry(new DeviceMetadataIndexEntry(deviceId, nodeOffset));
                }
                deviceNode.SetEndOffset(_fileStream.Position);
                tableIndexNodes[tableName] = deviceNode;
            }
        }
        else
        {
            // Tree model: each device path is a table
            // Top-level node must be device-level (matching Java's structure)
            foreach (var (deviceName, measurementNode) in deviceMeasurementNodes)
            {
                var deviceNode = new MetadataIndexNode(MetadataIndexNodeType.LeafDevice);
                // Serialize measurement node to get its offset
                var measurementNodeOffset = _fileStream.Position;
                measurementNode.Serialize(_writer);
                deviceNode.AddEntry(new DeviceMetadataIndexEntry(new StringArrayDeviceID(deviceName), measurementNodeOffset));
                deviceNode.SetEndOffset(_fileStream.Position);
                tableIndexNodes[deviceName] = deviceNode;
            }
        }

        // TsFileMetadata block starts here (this is what metadataSize measures)
        var tsFileMetadataStart = _fileStream.Position;

        // Write table index node map
        WriteUnsignedVarInt(tableIndexNodes.Count);
        foreach (var (tableName, indexNode) in tableIndexNodes)
        {
            WriteVarIntString(tableName);
            indexNode.Serialize(_writer);
        }

        // Write table schema map
        if (hasTableModel)
        {
            WriteUnsignedVarInt(_schemas.Count);
            foreach (var schema in _schemas.Values)
            {
                WriteVarIntString(schema.TableName);
                var columns = schema.ColumnSchemas ?? new List<ColumnSchema>();
                WriteUnsignedVarInt(columns.Count);
                foreach (var col in columns)
                {
                    WriteInt32PrefixedString(col.Name);
                    _writer.Write((byte)col.DataType);
                    _writer.Write((byte)col.Encoding);
                    _writer.Write((byte)col.Compression);
                    WriteInt32BigEndian(0); // props map count (empty)
                    WriteInt32BigEndian((int)col.Category);
                }
            }
        }
        else
        {
            // Tree model: no table schemas
            WriteUnsignedVarInt(0);
        }

        // Write metadata offset (big-endian) - points to SEPARATOR byte
        WriteLongBigEndian(metadataStartPos);

        // Write bloom filter (empty) - Java uses writeUnsignedVarInt
        WriteUnsignedVarInt(0);
        // Write properties count (empty) - Java uses writeVarInt (ZigZag)
        WriteZigZagVarInt(0);

        // Calculate and write TsFileMetadata size (big-endian, 4 bytes)
        // This measures only the TsFileMetadata block, NOT TimeseriesMetadata or intermediate nodes
        var metadataSize = (int)(_fileStream.Position - tsFileMetadataStart);
        WriteInt32BigEndian(metadataSize);
    }

    private void WriteTimeseriesMetadata(string measurementId, List<ChunkInfo> chunks)
    {
        // Aggregate statistics across all chunks
        long totalCount = 0;
        long startTime = long.MaxValue;
        long endTime = long.MinValue;
        var dataType = chunks[0].DataType;
        object? aggMin = chunks[0].MinValue;
        object? aggMax = chunks[0].MaxValue;
        object? aggFirst = chunks[0].FirstValue;
        object? aggLast = chunks[0].LastValue;
        double aggSum = 0;

        foreach (var chunk in chunks)
        {
            totalCount += chunk.Count;
            if (chunk.StartTime < startTime) { startTime = chunk.StartTime; aggFirst = chunk.FirstValue; }
            if (chunk.EndTime > endTime) { endTime = chunk.EndTime; aggLast = chunk.LastValue; }
            aggSum += chunk.SumValue;
            aggMin = AggregateMin(aggMin, chunk.MinValue, dataType);
            aggMax = AggregateMax(aggMax, chunk.MaxValue, dataType);
        }

        // Build ChunkMetadata list buffer
        using var chunkMetaBuffer = new MemoryStream();
        bool hasStatistics = chunks.Count > 1;
        foreach (var chunk in chunks)
        {
            // ChunkMetadata: [offsetOfChunkHeader:Int64BE][statistics if multiple chunks]
            WriteLongBigEndianToStream(chunkMetaBuffer, chunk.Offset);
            // For single chunk, no per-chunk statistics (they're in TimeseriesMetadata)
        }
        var chunkMetaBytes = chunkMetaBuffer.ToArray();

        // Build statistics buffer
        using var statsBuffer = new MemoryStream();
        WriteStatisticsV4(statsBuffer, dataType, totalCount, startTime, endTime, aggMin, aggMax, aggFirst, aggLast, aggSum);
        var statsBytes = statsBuffer.ToArray();

        // TimeseriesMetadata type byte:
        // Bit 7 (0x80): has chunk metadata list
        // Bits 0-5: 0 for non-aligned, chunk type flags for aligned
        byte tsMetaType = 0x00;
        if (chunks.Count > 0 && !chunks[0].IsTimeChunk)
            tsMetaType = 0x00; // non-aligned measurement
        // Set bit 7 to indicate we have chunk metadata
        // Java: timeSeriesMetadataType & 0x80 != 0 means has statistics in chunk metadata
        // For single chunk: type = 0 (no per-chunk stats)
        // For multiple chunks: type = 0x80 (has per-chunk stats)

        int chunkMetaDataListDataSize = chunkMetaBytes.Length;

        // Write: [type][measurementId:VarIntString][dataType][chunkMetaDataListDataSize:unsignedVarInt][statistics][chunkMetaList]
        _writer.Write(tsMetaType);
        WriteVarIntString(measurementId);
        _writer.Write((byte)dataType);
        WriteUnsignedVarInt(chunkMetaDataListDataSize);
        _writer.Write(statsBytes);
        _writer.Write(chunkMetaBytes);
    }

    private void WriteStatisticsV4(Stream stream, TsDataType dataType, long count, long startTime, long endTime,
        object? minValue, object? maxValue, object? firstValue, object? lastValue, double sumValue)
    {
        // Statistics format: [count:unsignedVarInt][startTime:Int64BE][endTime:Int64BE][type-specific stats]
        WriteUnsignedVarLongToStream(stream, count);
        WriteLongBigEndianToStream(stream, startTime);
        WriteLongBigEndianToStream(stream, endTime);

        switch (dataType)
        {
            case TsDataType.Boolean:
                // first(1) + last(1) + sum(8)
                stream.WriteByte((byte)(firstValue is true ? 1 : 0));
                stream.WriteByte((byte)(lastValue is true ? 1 : 0));
                WriteLongBigEndianToStream(stream, (long)sumValue);
                break;
            case TsDataType.Int32:
            case TsDataType.Date:
                // min(4) + max(4) + first(4) + last(4) + sum(8)
                WriteInt32BigEndianToStream(stream, minValue is int mi ? mi : 0);
                WriteInt32BigEndianToStream(stream, maxValue is int ma ? ma : 0);
                WriteInt32BigEndianToStream(stream, firstValue is int fi ? fi : 0);
                WriteInt32BigEndianToStream(stream, lastValue is int la ? la : 0);
                WriteDoubleBigEndianToStream(stream, sumValue);
                break;
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                // min(8) + max(8) + first(8) + last(8) + sum(8)
                WriteLongBigEndianToStream(stream, minValue is long mni ? mni : 0L);
                WriteLongBigEndianToStream(stream, maxValue is long mxa ? mxa : 0L);
                WriteLongBigEndianToStream(stream, firstValue is long fli ? fli : 0L);
                WriteLongBigEndianToStream(stream, lastValue is long lla ? lla : 0L);
                WriteDoubleBigEndianToStream(stream, sumValue);
                break;
            case TsDataType.Float:
                // min(4) + max(4) + first(4) + last(4) + sum(8)
                WriteFloatBigEndianToStream(stream, minValue is float fmn ? fmn : 0f);
                WriteFloatBigEndianToStream(stream, maxValue is float fmx ? fmx : 0f);
                WriteFloatBigEndianToStream(stream, firstValue is float ff ? ff : 0f);
                WriteFloatBigEndianToStream(stream, lastValue is float fl ? fl : 0f);
                WriteDoubleBigEndianToStream(stream, sumValue);
                break;
            case TsDataType.Double:
                // min(8) + max(8) + first(8) + last(8) + sum(8)
                WriteDoubleBigEndianToStream(stream, minValue is double dmn ? dmn : 0.0);
                WriteDoubleBigEndianToStream(stream, maxValue is double dmx ? dmx : 0.0);
                WriteDoubleBigEndianToStream(stream, firstValue is double df ? df : 0.0);
                WriteDoubleBigEndianToStream(stream, lastValue is double dl ? dl : 0.0);
                WriteDoubleBigEndianToStream(stream, sumValue);
                break;
            case TsDataType.Text:
            case TsDataType.String:
            {
                // first: [len:Int32BE][bytes], last: [len:Int32BE][bytes]
                var firstBytes = firstValue is string fs ? System.Text.Encoding.UTF8.GetBytes(fs) : Array.Empty<byte>();
                WriteInt32BigEndianToStream(stream, firstBytes.Length);
                if (firstBytes.Length > 0) stream.Write(firstBytes, 0, firstBytes.Length);
                var lastBytes = lastValue is string ls ? System.Text.Encoding.UTF8.GetBytes(ls) : Array.Empty<byte>();
                WriteInt32BigEndianToStream(stream, lastBytes.Length);
                if (lastBytes.Length > 0) stream.Write(lastBytes, 0, lastBytes.Length);
                break;
            }
            case TsDataType.Blob:
                // 0 bytes
                break;
        }
    }

    private static object? AggregateMin(object? a, object? b, TsDataType dataType)
    {
        if (a == null) return b;
        if (b == null) return a;
        return dataType switch
        {
            TsDataType.Int32 or TsDataType.Date => Math.Min((int)a, (int)b),
            TsDataType.Int64 or TsDataType.Timestamp => Math.Min((long)a, (long)b),
            TsDataType.Float => Math.Min((float)a, (float)b),
            TsDataType.Double => Math.Min((double)a, (double)b),
            _ => a
        };
    }

    private static object? AggregateMax(object? a, object? b, TsDataType dataType)
    {
        if (a == null) return b;
        if (b == null) return a;
        return dataType switch
        {
            TsDataType.Int32 or TsDataType.Date => Math.Max((int)a, (int)b),
            TsDataType.Int64 or TsDataType.Timestamp => Math.Max((long)a, (long)b),
            TsDataType.Float => Math.Max((float)a, (float)b),
            TsDataType.Double => Math.Max((double)a, (double)b),
            _ => a
        };
    }

    private static void WriteLongBigEndianToStream(Stream stream, long value)
    {
        stream.WriteByte((byte)(value >> 56));
        stream.WriteByte((byte)(value >> 48));
        stream.WriteByte((byte)(value >> 40));
        stream.WriteByte((byte)(value >> 32));
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteInt32BigEndianToStream(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteFloatBigEndianToStream(Stream stream, float value)
    {
        WriteInt32BigEndianToStream(stream, BitConverter.SingleToInt32Bits(value));
    }

    private static void WriteDoubleBigEndianToStream(Stream stream, double value)
    {
        WriteLongBigEndianToStream(stream, BitConverter.DoubleToInt64Bits(value));
    }
    
    private void WriteFooter()
    {
        // Write magic string again
        _writer.Write(TsFileConstants.MagicString);
    }
    
    private static void WriteInt64BigEndian(Stream stream, long value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        stream.Write(bytes, 0, 8);
    }

    private void WriteUnsignedVarInt(int value)
    {
        // Unsigned VarInt (no ZigZag) - matches Java ReadWriteForEncodingUtils.writeUnsignedVarInt
        while ((value & ~0x7F) != 0)
        {
            _writer.Write((byte)((value & 0x7F) | 0x80));
            value = (int)((uint)value >> 7);
        }
        _writer.Write((byte)value);
    }

    private void WriteZigZagVarInt(int value)
    {
        // ZigZag VarInt - matches Java ReadWriteForEncodingUtils.writeVarInt
        // ZigZag encode: (value << 1) ^ (value >> 31)
        int zigzag = (value << 1) ^ (value >> 31);
        WriteUnsignedVarInt(zigzag);
    }

    private void WriteVarIntString(string value)
    {
        // Matches Java ReadWriteIOUtils.writeVar which uses writeVarInt (ZigZag)
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteZigZagVarInt(bytes.Length);
        _writer.Write(bytes);
    }

    private void WriteLongBigEndian(long value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        _writer.Write(bytes);
    }

    private void WriteInt32BigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        _writer.Write(bytes);
    }

    private void WriteInt32PrefixedString(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteInt32BigEndian(bytes.Length);
        _writer.Write(bytes);
    }
    
    public void Dispose()
    {
        if (_disposed)
            return;

        if (!_closed)
        {
            Close();
        }

        _writer?.Dispose();
        _fileStream?.Dispose();

        foreach (var buffer in _deviceChunkBuffers.Values)
        {
            buffer?.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    // V4 format helper classes
    private class ChunkGroupInfo
    {
        public IDeviceID DeviceId { get; set; } = null!;
        public long StartOffset { get; set; }
        public long EndOffset { get; set; }
        public List<ChunkInfo> Chunks { get; set; } = new();
    }

    private class ChunkInfo
    {
        public string MeasurementId { get; set; } = string.Empty;
        public TsDataType DataType { get; set; }
        public TsEncoding Encoding { get; set; }
        public CompressionType Compression { get; set; }
        public long Offset { get; set; }
        public bool IsTimeChunk { get; set; }
        public long Count { get; set; }
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public object? MinValue { get; set; }
        public object? MaxValue { get; set; }
        public object? FirstValue { get; set; }
        public object? LastValue { get; set; }
        public double SumValue { get; set; }
    }
}
