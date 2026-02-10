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

using System.Collections;
using Apache.TsFile.Common;
using Apache.TsFile.Compress;
using Apache.TsFile.Encoding;
using Apache.TsFile.Enums;
using Apache.TsFile.Schema;

namespace Apache.TsFile.IO;

/// <summary>
/// Reader for reading TSFile format files.
/// </summary>
public class TsFileReader : IDisposable
{
    private readonly string _filePath;
    private readonly FileStream _fileStream;
    private readonly BinaryReader _reader;
    private Dictionary<string, TableSchema>? _schemas;
    private Dictionary<string, MetadataIndexNode>? _tableIndexNodes;
    private long _metadataOffset;
    private byte _fileVersion;
    private bool _disposed;
    
    /// <summary>
    /// Initializes a new instance of the TsFileReader class.
    /// </summary>
    public TsFileReader(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException("TSFile not found", filePath);
        
        _filePath = filePath;
        _fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        _reader = new BinaryReader(_fileStream);
        
        ValidateHeader();
        ReadMetadata();
    }
    
    /// <summary>
    /// Gets the file version (3 or 4).
    /// </summary>
    public byte FileVersion => _fileVersion;

    /// <summary>
    /// Gets the available table schemas in this file.
    /// </summary>
    public IReadOnlyDictionary<string, TableSchema> Schemas => _schemas!;
    
    /// <summary>
    /// Queries data from the file.
    /// </summary>
    public QueryResult Query(string deviceName, string[]? measurements = null,
        long? startTime = null, long? endTime = null)
    {
        if (!_schemas!.TryGetValue(deviceName, out var schema))
            throw new ArgumentException($"Device {deviceName} not found in file");

        // Simplified C# V3 files have no index nodes - use chunk scanning
        if (_tableIndexNodes == null || _tableIndexNodes.Count == 0)
        {
            return QueryV3Simplified(deviceName, schema, measurements, startTime, endTime);
        }

        return QueryV4(deviceName, schema, measurements, startTime, endTime);
    }
    
    /// <summary>
    /// Queries all data for a device.
    /// </summary>
    public Tablet QueryAll(string deviceName)
    {
        var result = Query(deviceName);
        return result.ToTablet();
    }
    
    private void ValidateHeader()
    {
        var magic = _reader.ReadBytes(TsFileConstants.MagicString.Length);
        
        if (!magic.SequenceEqual(TsFileConstants.MagicString))
            throw new InvalidDataException("Invalid TSFile magic string");
        
        _fileVersion = _reader.ReadByte();
        if (_fileVersion != TsFileConstants.Version && _fileVersion != TsFileConstants.JavaVersion4)
            throw new InvalidDataException($"Unsupported TSFile version: {_fileVersion}");
    }
    
    private void ReadMetadata()
    {
        if (_fileVersion == TsFileConstants.Version)
        {
            ReadMetadataV3();
        }
        else
        {
            ReadMetadataV4();
        }
    }

    private void ReadMetadataV4()
    {
        // Java V4 footer: [TsFileMetadata][metadataSize: Int32BE][MAGIC: 6 bytes]
        _fileStream.Seek(-4 - TsFileConstants.MagicString.Length, SeekOrigin.End);
        var metadataSize = ReadInt32BigEndian(_reader);

        var footerMagic = _reader.ReadBytes(TsFileConstants.MagicString.Length);
        if (!footerMagic.SequenceEqual(TsFileConstants.MagicString))
            throw new InvalidDataException("Invalid TSFile footer magic string");

        var metadataEndPos = _fileStream.Length - 4 - TsFileConstants.MagicString.Length;
        var metadataStartPos = metadataEndPos - metadataSize;

        _fileStream.Position = metadataStartPos;
        ReadTsFileMetadataV4();
    }

    private void ReadMetadataV3()
    {
        // Validate footer magic
        _fileStream.Seek(-TsFileConstants.MagicString.Length, SeekOrigin.End);
        var footerMagic = _reader.ReadBytes(TsFileConstants.MagicString.Length);
        if (!footerMagic.SequenceEqual(TsFileConstants.MagicString))
            throw new InvalidDataException("Invalid TSFile footer magic string");

        // Try Java V3 footer: [TsFileMetadata][metadataSize: Int32BE][MAGIC]
        // Read the 4 bytes before MAGIC as potential metadataSize
        _fileStream.Seek(-4 - TsFileConstants.MagicString.Length, SeekOrigin.End);
        var potentialMetadataSize = ReadInt32BigEndian(_reader);
        var metadataEndPos = _fileStream.Length - 4 - TsFileConstants.MagicString.Length;
        var metadataStartPos = metadataEndPos - potentialMetadataSize;

        if (potentialMetadataSize > 0 && metadataStartPos > TsFileConstants.MagicString.Length + 1)
        {
            try
            {
                _fileStream.Position = metadataStartPos;
                ReadTsFileMetadataV3Java();
                return;
            }
            catch
            {
                // Not Java V3 format, try C# simplified format
            }
        }

        // C# simplified V3 footer: [metadata][metadataOffset: Int64LE][MAGIC]
        _fileStream.Seek(-8 - TsFileConstants.MagicString.Length, SeekOrigin.End);
        _metadataOffset = _reader.ReadInt64();
        _fileStream.Position = _metadataOffset;
        ReadTsFileMetadataV3Simplified();
    }
    
    private void ReadTsFileMetadataV3Java()
    {
        // Java V3 metadata structure (from Java CompatibilityUtils.deserializeTsFileMetadataFromV3):
        // 1. Single MetadataIndexNode (device-level, using PlainDeviceID)
        // 2. metaOffset: Int64 (big-endian)
        // 3. bloomFilter (optional): [Int32BE(bytesLength)][bytes][uVarInt(filterSize)][uVarInt(hashFunctionSize)]

        // 1. Read single MetadataIndexNode (V3 uses PlainDeviceID for device entries)
        var indexNode = ReadMetadataIndexNodeV3(isDeviceLevel: true);
        _tableIndexNodes = new Dictionary<string, MetadataIndexNode>();
        // V3 stores the single index node under empty string key (same as Java)
        _tableIndexNodes[""] = indexNode;

        // V3 has no tableSchemaMap - create virtual schemas from index nodes
        _schemas = new Dictionary<string, TableSchema>();

        // Extract device names from the index tree and create virtual schemas
        var deviceNames = new HashSet<string>();
        CollectDeviceNames(indexNode, deviceNames);
        foreach (var deviceName in deviceNames)
        {
            // Use the full device path as table name for V3 tree model
            if (!_schemas.ContainsKey(deviceName))
            {
                var schema = new TableSchema(deviceName);
                _schemas[deviceName] = schema;
            }
        }

        // If no devices found (e.g., single measurement node), use empty key
        if (_schemas.Count == 0)
        {
            _schemas[""] = new TableSchema("default");
        }

        // 2. Read metadata offset
        _metadataOffset = ReadInt64BigEndian(_reader);

        // 3. Skip bloom filter if present (V3 uses Int32BE-prefixed bytes)
        if (_fileStream.Position < _fileStream.Length - 10)
        {
            var bloomFilterBytesLength = ReadInt32BigEndian(_reader);
            if (bloomFilterBytesLength > 0 && bloomFilterBytesLength < TsFileConstants.MaxBloomFilterSize)
            {
                _reader.ReadBytes(bloomFilterBytesLength);
                ReadUnsignedVarInt(); // filterSize
                ReadUnsignedVarInt(); // hashFunctionSize
            }
        }
    }

    private void ReadTsFileMetadataV3Simplified()
    {
        // Simplified C# V3 format:
        // [Int32LE(schemaCount)][schemas...][Int64LE(metadataOffset)]
        var schemaCount = _reader.ReadInt32();

        _schemas = new Dictionary<string, TableSchema>();
        _tableIndexNodes = new Dictionary<string, MetadataIndexNode>();

        for (int i = 0; i < schemaCount; i++)
        {
            var schema = TableSchema.Deserialize(_reader);
            _schemas[schema.TableName] = schema;
        }

        _metadataOffset = _reader.ReadInt64();
    }

    private void CollectDeviceNames(MetadataIndexNode node, HashSet<string> deviceNames)
    {
        foreach (var entry in node.Entries)
        {
            if (entry is DeviceMetadataIndexEntry deviceEntry)
            {
                deviceNames.Add(deviceEntry.DeviceID.ToString() ?? "");
            }
        }
    }

    private MetadataIndexNode ReadMetadataIndexNodeV3(bool isDeviceLevel)
    {
        return MetadataIndexNode.DeserializeV3(_reader, isDeviceLevel, ReadUnsignedVarInt, ReadVarIntString, () => ReadInt64BigEndian(_reader));
    }

    private MetadataIndexNode ReadMetadataIndexNodeForVersion(bool isDeviceLevel)
    {
        if (_fileVersion == TsFileConstants.Version)
            return ReadMetadataIndexNodeV3(isDeviceLevel);
        return ReadMetadataIndexNodeV4(isDeviceLevel);
    }

    private void ReadTsFileMetadataV4()
    {
        // V4 metadata structure (from Java TsFileMetadata.deserializeFrom):
        // NOTE: SEPARATOR marker is BEFORE TimeseriesMetadata, NOT before TsFileMetadata!
        // The metadataSize from file footer does NOT include SEPARATOR.
        // 1. tableIndexNodeMap: VarInt(count) + [VarIntString(tableName) + MetadataIndexNode]...
        // 2. tableSchemaMap: VarInt(count) + [VarIntString(tableName) + TableSchema]...
        // 3. metaOffset: Int64 (big-endian)
        // 4. bloomFilter (optional)
        // 5. properties (optional)

        try
        {
            // 1. Read table index node map (needed for V4 queries)
            // NOTE: Use ReadUnsignedVarInt for counts (no ZigZag)
            var tableIndexNodeNum = ReadUnsignedVarInt();
            _tableIndexNodes = new Dictionary<string, MetadataIndexNode>();

            for (int i = 0; i < tableIndexNodeNum; i++)
            {
                // NOTE: ReadVarIntString uses ReadVarInt internally (with ZigZag)
                var tableName = ReadVarIntString();
                var indexNode = ReadMetadataIndexNodeV4(isDeviceLevel: true);
                _tableIndexNodes[tableName] = indexNode;
            }

            // 2. Read table schemas
            // NOTE: Use ReadUnsignedVarInt for counts (no ZigZag)
            var tableSchemaNum = ReadUnsignedVarInt();
            _schemas = new Dictionary<string, TableSchema>();

            for (int i = 0; i < tableSchemaNum; i++)
            {
                // NOTE: ReadVarIntString uses ReadVarInt internally (with ZigZag)
                var tableName = ReadVarIntString();
                var tableSchema = ReadTableSchemaV4(tableName);
                _schemas[tableName] = tableSchema;
            }

            // Tree Model V4 files have no table schemas (tableSchemaNum = 0)
            // In this case, create virtual schemas from table index nodes
            if (tableSchemaNum == 0 && _tableIndexNodes != null && _tableIndexNodes.Count > 0)
            {
                foreach (var kvp in _tableIndexNodes)
                {
                    var tableName = kvp.Key;
                    // Create empty schema - measurements will be populated during query
                    var schema = new TableSchema(tableName);
                    _schemas[tableName] = schema;
                }
            }

            // 3. Read metadata offset
            _metadataOffset = ReadInt64BigEndian(_reader);

            // 4. Skip bloom filter if present
            if (_fileStream.Position < _fileStream.Length - 10)
            {
                var bloomFilterBytesLength = ReadVarInt();
                if (bloomFilterBytesLength > 0 && bloomFilterBytesLength < TsFileConstants.MaxBloomFilterSize)
                {
                    _reader.ReadBytes(bloomFilterBytesLength);
                    ReadVarInt(); // filterSize
                    ReadVarInt(); // hashFunctionSize
                }
            }

            // 5. Skip properties if present
            if (_fileStream.Position < _fileStream.Length - 10)
            {
                var propertiesSize = ReadVarInt();
                for (int i = 0; i < propertiesSize && i < TsFileConstants.MaxPropertiesCount; i++)
                {
                    ReadVarIntString(); // key
                    ReadVarIntString(); // value
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                $"Failed to parse v4 metadata. V4 format support is still experimental. Error: {ex.Message}", ex);
        }
    }

    private MetadataIndexNode ReadMetadataIndexNodeV4(bool isDeviceLevel)
    {
        // NOTE: Java uses readUnsignedVarInt for entry count (no ZigZag)
        return MetadataIndexNode.DeserializeV4(_reader, isDeviceLevel, ReadUnsignedVarInt, ReadVarIntString, () => ReadInt64BigEndian(_reader));
    }

    private TableSchema ReadTableSchemaV4(string tableName)
    {
        // TableSchema format (from Java TableSchema.deserialize):
        // - VarInt: column count (NOTE: Java uses readUnsignedVarInt, no ZigZag)
        // - For each column:
        //   - MeasurementSchema (Int32-prefixed strings)
        //   - Int32 (big-endian): columnCategory ordinal

        var columnCount = ReadUnsignedVarInt();
        var tableSchema = new TableSchema(tableName);
        tableSchema.ColumnSchemas = new List<ColumnSchema>();

        for (int j = 0; j < columnCount; j++)
        {
            // Read MeasurementSchema (Java format uses Int32 length prefix for strings)
            var columnName = ReadInt32PrefixedString();
            var dataType = (TsDataType)_reader.ReadByte();
            var encoding = (TsEncoding)_reader.ReadByte();
            var compression = (CompressionType)_reader.ReadByte();

            // Skip props map
            var propsCount = ReadInt32BigEndian(_reader);
            for (int k = 0; k < propsCount; k++)
            {
                ReadInt32PrefixedString(); // key
                ReadInt32PrefixedString(); // value
            }

            // Read column category (Int32, big-endian)
            var categoryOrdinal = ReadInt32BigEndian(_reader);
            var category = (ColumnCategory)categoryOrdinal;

            var columnSchema = new ColumnSchema(columnName, category, dataType, encoding, compression);
            tableSchema.ColumnSchemas.Add(columnSchema);

            // Add FIELD columns to Measurements for compatibility
            if (category == ColumnCategory.Field)
            {
                tableSchema.AddMeasurement(new MeasurementSchema(columnName, dataType, encoding, compression));
            }
        }

        return tableSchema;
    }

    private string ReadInt32PrefixedString()
    {
        // Java's ReadWriteIOUtils.write(String) uses Int32 (big-endian) length prefix
        var length = ReadInt32BigEndian(_reader);
        if (length < 0)
            return string.Empty;
        var bytes = _reader.ReadBytes(length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
    
    private int ReadUnsignedVarInt()
    {
        // Read unsigned variable-length integer (no ZigZag decoding)
        // (similar to ReadWriteForEncodingUtils.readUnsignedVarInt)
        int value = 0;
        int shift = 0;
        byte b;

        do
        {
            b = _reader.ReadByte();
            value |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);

        return value;
    }

    private int ReadVarInt()
    {
        // Read signed variable-length integer with ZigZag decoding
        // (similar to ReadWriteForEncodingUtils.readVarInt)

        // First read unsigned VarInt
        int value = ReadUnsignedVarInt();

        // Then apply ZigZag decoding (Java: value >>> 1)
        // In C#, use unsigned right shift: (int)((uint)value >> 1)
        int x = (int)((uint)value >> 1);
        if ((value & 1) != 0)
        {
            x = ~x;
        }
        return x;
    }
    
    private string ReadVarIntString()
    {
        // Read variable-length string (similar to ReadWriteIOUtils.readVarIntString)
        var length = ReadVarInt();
        if (length <= 0)
            return string.Empty;
        var bytes = _reader.ReadBytes(length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
    
    private int ReadInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }
    
    private long ReadInt64BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }
    
    private void ReadChunk(QueryResult result, TableSchema schema, 
        string[]? measurements, long? startTime, long? endTime)
    {
        var measurementFilter = measurements?.ToHashSet() ?? null;
        
        // Read each measurement column
        for (int i = 0; i < schema.Measurements.Count; i++)
        {
            var measurementName = _reader.ReadString();
            var dataType = (TsDataType)_reader.ReadByte();
            var encoding = (TsEncoding)_reader.ReadByte();
            var compression = (CompressionType)_reader.ReadByte();
            
            var compressedSize = _reader.ReadInt32();
            var originalSize = _reader.ReadInt32();
            var compressedData = _reader.ReadBytes(compressedSize);
            
            // Decompress and decode if this measurement is requested
            if (measurementFilter == null || measurementFilter.Contains(measurementName))
            {
                var uncompressor = CompressorFactory.GetUncompressor(compression);
                var decodedData = uncompressor.Uncompress(compressedData);
                
                var decoder = DecoderFactory.CreateDecoder(encoding, dataType);
                var values = DecodeColumn(decoder, dataType, decodedData);
                
                result.AddMeasurementData(measurementName, values);
            }
        }
        
        // Read timestamps
        var timestampSize = _reader.ReadInt32();
        var timestampData = _reader.ReadBytes(timestampSize);
        var rowCount = _reader.ReadInt32();
        
        var timestamps = new long[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            timestamps[i] = ReadInt64BigEndian(timestampData, i * 8);
        }
        
        // Truncate measurement data to match timestamp count
        // Some decoders (e.g. Gorilla V2) may decode extra values from padding bits
        result.TruncateMeasurementData(rowCount);
        
        // Filter by time range if specified
        if (startTime.HasValue || endTime.HasValue)
        {
            var filteredTimestamps = new List<long>();
            var filteredIndices = new List<int>();
            
            for (int i = 0; i < timestamps.Length; i++)
            {
                if ((!startTime.HasValue || timestamps[i] >= startTime.Value) &&
                    (!endTime.HasValue || timestamps[i] <= endTime.Value))
                {
                    filteredTimestamps.Add(timestamps[i]);
                    filteredIndices.Add(i);
                }
            }
            
            result.AddTimestamps(filteredTimestamps.ToArray(), filteredIndices);
        }
        else
        {
            result.AddTimestamps(timestamps, null);
        }
    }
    
    private void SkipChunk(TableSchema schema)
    {
        for (int i = 0; i < schema.Measurements.Count; i++)
        {
            _reader.ReadString(); // measurement name
            _reader.ReadByte(); // data type
            _reader.ReadByte(); // encoding
            _reader.ReadByte(); // compression
            
            var compressedSize = _reader.ReadInt32();
            _reader.ReadInt32(); // original size
            _reader.ReadBytes(compressedSize); // skip data
        }
        
        var timestampSize = _reader.ReadInt32();
        _reader.ReadBytes(timestampSize); // skip timestamps
        _reader.ReadInt32(); // row count
    }
    
    private static List<object> DecodeColumn(IDecoder decoder, TsDataType dataType, byte[] data,
        TsEncoding encoding = TsEncoding.Plain)
    {
        var values = new List<object>();
        int offset = 0;
        
        // Java FloatEncoder wraps TS_2DIFF/RLE for float/double with maxPointNumber prefix
        // Format: [maxPointNumber: uVarInt][encoded int/long data]
        // Values are stored as round(value * 10^maxPointNumber) as int (float) or long (double)
        bool isFloatWrapped = (dataType == TsDataType.Float || dataType == TsDataType.Double) &&
                              (encoding == TsEncoding.Ts2Diff || encoding == TsEncoding.Rle);
        
        if (isFloatWrapped)
        {
            return DecodeFloatWrappedColumn(decoder, dataType, data);
        }
        
        while (decoder.HasNext(data, offset))
        {
            object value = dataType switch
            {
                TsDataType.Boolean => decoder.ReadBoolean(data, ref offset),
                TsDataType.Int32 => decoder.ReadInt(data, ref offset),
                TsDataType.Int64 or TsDataType.Timestamp => decoder.ReadLong(data, ref offset),
                TsDataType.Float => decoder.ReadFloat(data, ref offset),
                TsDataType.Double => decoder.ReadDouble(data, ref offset),
                TsDataType.Text or TsDataType.String => decoder.ReadString(data, ref offset),
                _ => throw new NotSupportedException($"Data type {dataType} not supported")
            };
            
            values.Add(value);
        }
        
        return values;
    }
    
    private static List<object> DecodeFloatWrappedColumn(IDecoder decoder, TsDataType dataType, byte[] data)
    {
        var values = new List<object>();
        int offset = 0;
        
        // Read maxPointNumber prefix
        int maxPointNumber = ReadUnsignedVarIntFromBytes(data, ref offset);
        
        // Check for overflow bitmap flags (Java FloatEncoder)
        BitArray? isUnderflowInfo = null;
        BitArray? valueItselfOverflowInfo = null;
        
        if (maxPointNumber == int.MaxValue)
        {
            // Has underflow bitmap only
            int size = ReadUnsignedVarIntFromBytes(data, ref offset);
            isUnderflowInfo = ReadBitMap(data, ref offset, size);
            maxPointNumber = ReadUnsignedVarIntFromBytes(data, ref offset);
        }
        else if (maxPointNumber == int.MaxValue - 1)
        {
            // Has both underflow and value-itself-overflow bitmaps
            int size = ReadUnsignedVarIntFromBytes(data, ref offset);
            isUnderflowInfo = ReadBitMap(data, ref offset, size);
            valueItselfOverflowInfo = ReadBitMap(data, ref offset, size);
            maxPointNumber = ReadUnsignedVarIntFromBytes(data, ref offset);
        }
        
        double maxPointValue = maxPointNumber <= 0 ? 1.0 : Math.Pow(10, maxPointNumber);
        
        // Create a sub-buffer for the actual encoded data
        byte[] encodedData = new byte[data.Length - offset];
        Array.Copy(data, offset, encodedData, 0, encodedData.Length);
        
        int subOffset = 0;
        int position = 0;
        
        if (dataType == TsDataType.Float)
        {
            while (decoder.HasNext(encodedData, subOffset))
            {
                int intValue = decoder.ReadInt(encodedData, ref subOffset);
                float result;
                if (valueItselfOverflowInfo != null && position < valueItselfOverflowInfo.Count && valueItselfOverflowInfo[position])
                {
                    result = BitConverter.Int32BitsToSingle(intValue);
                }
                else
                {
                    // When no overflow bitmaps exist, all values were scaled by maxPointValue
                    // When bitmaps exist, underflowInfo[i]=true means value was scaled, false means it was rounded without scaling
                    double divisor;
                    if (isUnderflowInfo == null)
                        divisor = maxPointValue; // no overflow: all values scaled
                    else if (position < isUnderflowInfo.Count && isUnderflowInfo[position])
                        divisor = maxPointValue; // bitmap set: value was scaled
                    else
                        divisor = 1.0; // bitmap not set: value was rounded without scaling
                    result = (float)(intValue / divisor);
                }
                values.Add(result);
                position++;
            }
        }
        else // Double
        {
            while (decoder.HasNext(encodedData, subOffset))
            {
                long longValue = decoder.ReadLong(encodedData, ref subOffset);
                double result;
                if (valueItselfOverflowInfo != null && position < valueItselfOverflowInfo.Count && valueItselfOverflowInfo[position])
                {
                    result = BitConverter.Int64BitsToDouble(longValue);
                }
                else
                {
                    double divisor;
                    if (isUnderflowInfo == null)
                        divisor = maxPointValue;
                    else if (position < isUnderflowInfo.Count && isUnderflowInfo[position])
                        divisor = maxPointValue;
                    else
                        divisor = 1.0;
                    result = longValue / divisor;
                }
                values.Add(result);
                position++;
            }
        }
        
        return values;
    }
    
    private static BitArray ReadBitMap(byte[] data, ref int offset, int size)
    {
        int byteCount = size / 8 + 1;
        var bits = new BitArray(size);
        for (int i = 0; i < size && (i / 8) < byteCount; i++)
        {
            int byteIdx = i / 8;
            int bitIdx = i % 8;
            if ((data[offset + byteIdx] & (1 << (7 - bitIdx))) != 0)
                bits[i] = true;
        }
        offset += byteCount;
        return bits;
    }
    
    private static long ReadInt64BigEndian(byte[] buffer, int offset)
    {
        return ((long)buffer[offset] << 56)
             | ((long)buffer[offset + 1] << 48)
             | ((long)buffer[offset + 2] << 40)
             | ((long)buffer[offset + 3] << 32)
             | ((long)buffer[offset + 4] << 24)
             | ((long)buffer[offset + 5] << 16)
             | ((long)buffer[offset + 6] << 8)
             | buffer[offset + 7];
    }

    #region V3 Simplified Query (C#-written V3 files)

    private QueryResult QueryV3Simplified(string deviceName, TableSchema schema, string[]? measurements,
        long? startTime, long? endTime)
    {
        var result = new QueryResult(deviceName, schema);

        // Scan chunks from data start
        _fileStream.Position = TsFileConstants.MagicString.Length + 1; // Skip header

        while (_fileStream.Position < _metadataOffset)
        {
            var marker = _reader.ReadByte();
            if (marker != TsFileConstants.ChunkHeaderMarker)
                break;

            var chunkDeviceName = _reader.ReadString();

            if (chunkDeviceName == deviceName)
            {
                ReadChunk(result, schema, measurements, startTime, endTime);
            }
            else
            {
                SkipChunk(schema);
            }
        }

        return result;
    }

    #endregion

    #region V4 Query Implementation

    private QueryResult QueryV4(string deviceName, TableSchema schema, string[]? measurements,
        long? startTime, long? endTime)
    {
        var result = new QueryResult(deviceName, schema);

        MetadataIndexNode? rootNode;
        if (_fileVersion == TsFileConstants.Version)
        {
            // V3: single root index node stored under empty key
            if (!_tableIndexNodes!.TryGetValue("", out rootNode))
                return result;
        }
        else
        {
            // V4: table name is the device name
            if (!_tableIndexNodes!.TryGetValue(deviceName, out rootNode))
                return result;
        }

        // Navigate to find timeseries metadata for this device
        var timeseriesMetadataList = NavigateToTimeseriesMetadata(rootNode, deviceName, measurements);

        // For Tree Model files (no table schemas), dynamically populate measurements from TimeseriesMetadata
        if (schema.Measurements.Count == 0)
        {
            foreach (var tsMetadata in timeseriesMetadataList)
            {
                var measurementSchema = new MeasurementSchema(
                    tsMetadata.MeasurementId,
                    tsMetadata.DataType,
                    TsEncoding.Plain, // Default encoding
                    CompressionType.Uncompressed); // Default compression
                schema.AddMeasurement(measurementSchema);
            }
        }

        // Read chunks for each timeseries
        foreach (var tsMetadata in timeseriesMetadataList)
        {
            ReadTimeseriesDataV4(result, tsMetadata, startTime, endTime);
        }

        // Truncate measurement data to match timestamp count
        // Some decoders (e.g. Gorilla V2) may decode extra values from padding bits
        if (result.Timestamps.Count > 0)
        {
            result.TruncateMeasurementData(result.Timestamps.Count);
        }

        return result;
    }

    private List<TimeseriesMetadataV4> NavigateToTimeseriesMetadata(MetadataIndexNode rootNode,
        string deviceName, string[]? measurements)
    {
        var result = new List<TimeseriesMetadataV4>();
        var measurementFilter = measurements?.ToHashSet();

        // For V3, deviceName is the full device path (e.g., "root.test.d0")
        // We need to filter to the specific device in the index tree
        string? deviceFilter = (_fileVersion == TsFileConstants.Version) ? deviceName : null;

        NavigateNode(rootNode, result, measurementFilter, deviceFilter);

        return result;
    }

    private void NavigateNode(MetadataIndexNode node, List<TimeseriesMetadataV4> result,
        HashSet<string>? measurementFilter, string? deviceFilter)
    {
        switch (node.NodeType)
        {
            case MetadataIndexNodeType.InternalDevice:
                // Internal device node: entries point to child device nodes
                foreach (var entry in node.Entries)
                {
                    if (entry is DeviceMetadataIndexEntry deviceEntry)
                    {
                        _fileStream.Position = deviceEntry.Offset;
                        var childNode = ReadMetadataIndexNodeForVersion(isDeviceLevel: true);
                        NavigateNode(childNode, result, measurementFilter, deviceFilter);
                    }
                }
                break;

            case MetadataIndexNodeType.LeafDevice:
                // Leaf device node: entries point to measurement index nodes
                foreach (var entry in node.Entries)
                {
                    if (entry is DeviceMetadataIndexEntry deviceEntry)
                    {
                        // For V3, filter to the specific device being queried
                        if (deviceFilter != null)
                        {
                            var entryDeviceName = deviceEntry.DeviceID.ToString();
                            if (entryDeviceName != deviceFilter)
                                continue;
                        }

                        _fileStream.Position = deviceEntry.Offset;
                        var measurementNode = ReadMetadataIndexNodeForVersion(isDeviceLevel: false);
                        NavigateNode(measurementNode, result, measurementFilter, deviceFilter);
                    }
                }
                break;

            case MetadataIndexNodeType.InternalMeasurement:
                // Internal measurement node: entries point to child measurement nodes
                foreach (var entry in node.Entries)
                {
                    if (entry is MeasurementMetadataIndexEntry measurementEntry)
                    {
                        if (measurementFilter != null && !measurementFilter.Contains(measurementEntry.Name))
                            continue;
                        _fileStream.Position = measurementEntry.Offset;
                        var childNode = ReadMetadataIndexNodeForVersion(isDeviceLevel: false);
                        NavigateNode(childNode, result, measurementFilter, deviceFilter);
                    }
                }
                break;

            case MetadataIndexNodeType.LeafMeasurement:
                // Leaf measurement node: entries point to TimeseriesMetadata
                ReadTimeseriesMetadataFromNode(node, result, measurementFilter);
                break;
        }
    }

    private void ReadTimeseriesMetadataFromNode(MetadataIndexNode node, List<TimeseriesMetadataV4> result,
        HashSet<string>? measurementFilter)
    {
        foreach (var entry in node.Entries)
        {
            if (entry is MeasurementMetadataIndexEntry measurementEntry)
            {
                // Check measurement filter
                if (measurementFilter != null && !measurementFilter.Contains(measurementEntry.Name))
                    continue;

                // Read timeseries metadata at this offset
                _fileStream.Position = measurementEntry.Offset;
                var tsMetadata = TimeseriesMetadataV4.Deserialize(_reader, ReadUnsignedVarInt, ReadVarIntString,
                    () => ReadInt64BigEndian(_reader), needChunkMetadata: true);
                result.Add(tsMetadata);
            }
        }
    }

    private void ReadTimeseriesDataV4(QueryResult result, TimeseriesMetadataV4 tsMetadata,
        long? startTime, long? endTime)
    {
        foreach (var chunkMeta in tsMetadata.ChunkMetadataList)
        {
            // Use statistics for time range filtering if available
            var stats = chunkMeta.Statistics ?? tsMetadata.Statistics;
            if (stats != null)
            {
                if (startTime.HasValue && stats.EndTime < startTime.Value)
                    continue; // Skip chunk - all data before start time
                if (endTime.HasValue && stats.StartTime > endTime.Value)
                    continue; // Skip chunk - all data after end time
            }

            // Read chunk data
            ReadChunkV4(result, chunkMeta, tsMetadata.DataType, startTime, endTime);
        }
    }

    private void ReadChunkV4(QueryResult result, ChunkMetadataV4 chunkMeta, TsDataType dataType,
        long? startTime, long? endTime)
    {
        _fileStream.Position = chunkMeta.OffsetOfChunkHeader;

        // Read chunk header marker
        var marker = _reader.ReadByte();
        
        // Handle ChunkGroupHeader (marker 0x00) in tree model files
        if (marker == 0x00)
        {
            if (_fileVersion == TsFileConstants.Version)
            {
                // V3: PlainDeviceID is just a VarIntString
                ReadVarIntString();
            }
            else
            {
                // V4: StringArrayDeviceID = unsignedVarInt(segCount) + VarIntString(segments)...
                var segCount = ReadUnsignedVarInt();
                for (int i = 0; i < segCount; i++)
                    ReadVarIntString();
            }
            // Read the actual chunk header marker
            marker = _reader.ReadByte();
        }
        
        // Valid markers (from Java MetaMarker):
        // 0x01 = CHUNK_HEADER (multi-page)
        // 0x05 = ONLY_ONE_PAGE_CHUNK_HEADER (single-page)
        // 0x81 = TIME_CHUNK_HEADER (aligned time, multi-page)
        // 0x85 = ONLY_ONE_PAGE_TIME_CHUNK_HEADER (aligned time, single-page)
        // 0x41 = VALUE_CHUNK_HEADER (aligned value, multi-page)
        // 0x45 = ONLY_ONE_PAGE_VALUE_CHUNK_HEADER (aligned value, single-page)
        var baseMarker = (byte)(marker & 0x3F); // Strip time/value mask bits
        if (baseMarker != 0x01 && baseMarker != 0x05)
            return;

        bool isTimeChunk = (marker & 0x80) != 0;
        bool isValueChunk = (marker & 0x40) != 0;
        bool isAligned = isTimeChunk || isValueChunk;
        bool hasMultiplePages = baseMarker == 0x01;

        // Read chunk header fields (Java ChunkHeader format)
        var measurementId = ReadVarIntString();
        var dataSize = ReadUnsignedVarInt(); // Java: readUnsignedVarInt
        var chunkDataType = (TsDataType)_reader.ReadByte();
        var compression = (CompressionType)_reader.ReadByte();
        var encoding = (TsEncoding)_reader.ReadByte();

        // Read chunk data (pages)
        var chunkDataStart = _fileStream.Position;
        var chunkDataEnd = chunkDataStart + dataSize;

        while (_fileStream.Position < chunkDataEnd)
        {
            ReadPageV4(result, measurementId, chunkDataType, encoding, compression,
                startTime, endTime, isAligned, isTimeChunk, hasMultiplePages);
        }
    }

    private void ReadPageV4(QueryResult result, string measurementId, TsDataType dataType,
        TsEncoding encoding, CompressionType compression, long? startTime, long? endTime,
        bool isAligned, bool isTimeChunk, bool hasMultiplePages)
    {
        // Read page header (Java PageHeader format)
        // uncompressedSize and compressedSize are unsignedVarInt
        var uncompressedSize = ReadUnsignedVarInt();
        if (uncompressedSize == 0)
            return; // Empty page
        var compressedSize = ReadUnsignedVarInt();

        // For multi-page chunks, page header includes statistics; skip them
        if (hasMultiplePages)
        {
            SkipStatisticsV4(dataType);
        }

        // Read compressed page data
        var compressedData = _reader.ReadBytes(compressedSize);

        // Decompress using known uncompressedSize from page header
        // (Java format does NOT prepend size to compressed data, unlike C# LZ4 wrapper)
        byte[] pageData;
        if (compression == CompressionType.Uncompressed)
            pageData = compressedData;
        else
            pageData = DecompressPageData(compressedData, uncompressedSize, compression);

        if (isAligned)
        {
            // Aligned (table model) format: time and value are in separate chunks
            if (isTimeChunk)
            {
                // Decode timestamps from time chunk
                var timeDecoder = DecoderFactory.CreateDecoder(encoding, TsDataType.Int64);
                var timestamps = DecodeTimestamps(timeDecoder, pageData);
                result.AddTimestamps(timestamps, null);
            }
            else
            {
                // Decode values from value chunk
                var decoder = DecoderFactory.CreateDecoder(encoding, dataType);
                var values = DecodeColumn(decoder, dataType, pageData, encoding);
                if (values.Count > 0)
                    result.AddMeasurementData(measurementId, values);
            }
        }
        else
        {
            // Non-aligned (tree model) format: timestamps and values in same page
            // Page data: [timeBufferLength: unsignedVarInt][timeBuffer][valueBuffer]
            int offset = 0;
            int timeBufferLength = ReadUnsignedVarIntFromBytes(pageData, ref offset);
            
            var timeBuffer = new byte[timeBufferLength];
            Array.Copy(pageData, offset, timeBuffer, 0, timeBufferLength);
            
            var valueBuffer = new byte[pageData.Length - offset - timeBufferLength];
            Array.Copy(pageData, offset + timeBufferLength, valueBuffer, 0, valueBuffer.Length);

            // Decode timestamps (always TS_2DIFF/INT64 for time column)
            var timeDecoder = DecoderFactory.CreateDecoder(TsEncoding.Ts2Diff, TsDataType.Int64);
            var timestamps = DecodeTimestamps(timeDecoder, timeBuffer);

            // Decode values
            var valueDecoder = DecoderFactory.CreateDecoder(encoding, dataType);
            var values = DecodeColumn(valueDecoder, dataType, valueBuffer, encoding);

            // Apply time range filter
            if (startTime.HasValue || endTime.HasValue)
            {
                var filteredTimestamps = new List<long>();
                var filteredValues = new List<object>();
                for (int i = 0; i < timestamps.Length && i < values.Count; i++)
                {
                    if ((!startTime.HasValue || timestamps[i] >= startTime.Value) &&
                        (!endTime.HasValue || timestamps[i] <= endTime.Value))
                    {
                        filteredTimestamps.Add(timestamps[i]);
                        filteredValues.Add(values[i]);
                    }
                }
                if (filteredTimestamps.Count > 0)
                {
                    result.AddTimestamps(filteredTimestamps.ToArray(), null);
                    result.AddMeasurementData(measurementId, filteredValues);
                }
            }
            else
            {
                result.AddTimestamps(timestamps, null);
                if (values.Count > 0)
                    result.AddMeasurementData(measurementId, values);
            }
        }
    }

    private long[] DecodeTimestamps(IDecoder decoder, byte[] data)
    {
        var timestamps = new List<long>();
        int offset = 0;
        while (decoder.HasNext(data, offset))
        {
            timestamps.Add(decoder.ReadLong(data, ref offset));
        }
        return timestamps.ToArray();
    }

    private static int ReadUnsignedVarIntFromBytes(byte[] data, ref int offset)
    {
        int value = 0;
        int shift = 0;
        byte b;
        do
        {
            b = data[offset++];
            value |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return value;
    }

    private static byte[] DecompressPageData(byte[] compressedData, int uncompressedSize, CompressionType compression)
    {
        // Java TsFile format does NOT prepend size to compressed data.
        // The C# LZ4 wrapper expects a 4-byte size prefix, so we handle LZ4 specially.
        switch (compression)
        {
            case CompressionType.Lz4:
                var output = new byte[uncompressedSize];
                K4os.Compression.LZ4.LZ4Codec.Decode(
                    compressedData, 0, compressedData.Length,
                    output, 0, uncompressedSize);
                return output;
            default:
                var uncompressor = CompressorFactory.GetUncompressor(compression);
                return uncompressor.Uncompress(compressedData);
        }
    }

    private void SkipStatisticsV4(TsDataType dataType)
    {
        // Skip count (unsignedVarInt)
        ReadUnsignedVarInt();
        // Skip startTime and endTime (2 x Int64 big-endian)
        _reader.ReadBytes(16);
        // Skip type-specific statistics (sizes from Java *Statistics.getStatsSize())
        switch (dataType)
        {
            case TsDataType.Boolean:
                _reader.ReadBytes(10); // first(1) + last(1) + sum(8)
                break;
            case TsDataType.Int32:
            case TsDataType.Date:
                _reader.ReadBytes(24); // min(4) + max(4) + first(4) + last(4) + sum(8)
                break;
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                _reader.ReadBytes(40); // min(8) + max(8) + first(8) + last(8) + sum(8)
                break;
            case TsDataType.Float:
                _reader.ReadBytes(24); // min(4) + max(4) + first(4) + last(4) + sum(8)
                break;
            case TsDataType.Double:
                _reader.ReadBytes(40); // min(8) + max(8) + first(8) + last(8) + sum(8)
                break;
            case TsDataType.Text:
            case TsDataType.String:
                // first(4+len) + last(4+len), no min/max
                for (int i = 0; i < 2; i++)
                {
                    var len = ReadInt32BigEndian(_reader);
                    if (len > 0) _reader.ReadBytes(len);
                }
                break;
            case TsDataType.Blob:
                // 0 bytes (no stats)
                break;
        }
    }

    #endregion
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _reader?.Dispose();
        _fileStream?.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Represents query results from a TSFile.
/// </summary>
public class QueryResult
{
    public string DeviceName { get; }
    public TableSchema Schema { get; }
    public List<long> Timestamps { get; }
    public Dictionary<string, List<object>> MeasurementData { get; }
    
    internal QueryResult(string deviceName, TableSchema schema)
    {
        DeviceName = deviceName;
        Schema = schema;
        Timestamps = new List<long>();
        MeasurementData = new Dictionary<string, List<object>>();
    }
    
    internal void AddTimestamps(long[] timestamps, List<int>? indices)
    {
        if (indices == null)
        {
            Timestamps.AddRange(timestamps);
        }
        else
        {
            foreach (var index in indices)
            {
                Timestamps.Add(timestamps[index]);
            }
        }
    }
    
    internal void AddMeasurementData(string measurement, List<object> values)
    {
        if (!MeasurementData.ContainsKey(measurement))
        {
            MeasurementData[measurement] = new List<object>();
        }
        
        MeasurementData[measurement].AddRange(values);
    }
    
    internal void TruncateMeasurementData(int maxCount)
    {
        foreach (var key in MeasurementData.Keys)
        {
            var list = MeasurementData[key];
            if (list.Count > maxCount)
            {
                list.RemoveRange(maxCount, list.Count - maxCount);
            }
        }
    }
    
    public Tablet ToTablet()
    {
        var tablet = new Tablet(DeviceName, Schema.Measurements, Timestamps.Count);
        
        for (int i = 0; i < Timestamps.Count; i++)
        {
            var values = new object[Schema.Measurements.Count];
            for (int j = 0; j < Schema.Measurements.Count; j++)
            {
                var measurementName = Schema.Measurements[j].MeasurementName;
                values[j] = MeasurementData[measurementName][i];
            }
            
            tablet.AddRow(Timestamps[i], values);
        }
        
        return tablet;
    }
}
