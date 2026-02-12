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

namespace Apache.TsFile.IO;

/// <summary>
/// Timeseries metadata for TsFile v4 format.
/// Contains metadata about a single timeseries including its chunks.
/// </summary>
public class TimeseriesMetadataV4
{
    /// <summary>
    /// Bit mask indicating this chunk has more than one page (needs page-level statistics).
    /// </summary>
    public const byte HasMultiplePages = 0x01;
    
    /// <summary>
    /// Bit mask indicating this is a time chunk of a vector series.
    /// </summary>
    public const byte TimeChunkMask = 0x80;
    
    /// <summary>
    /// Bit mask indicating this is a value chunk of a vector series.
    /// </summary>
    public const byte ValueChunkMask = 0x40;
    
    /// <summary>
    /// Gets the type byte for this timeseries metadata.
    /// </summary>
    public byte TimeSeriesMetadataType { get; set; }
    
    /// <summary>
    /// Gets the measurement ID.
    /// </summary>
    public string MeasurementId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets the data type.
    /// </summary>
    public TsDataType DataType { get; set; }
    
    /// <summary>
    /// Gets the size of the chunk metadata list data.
    /// </summary>
    public int ChunkMetadataListDataSize { get; set; }
    
    /// <summary>
    /// Gets the statistics for this timeseries.
    /// </summary>
    public StatisticsV4? Statistics { get; set; }
    
    /// <summary>
    /// Gets the list of chunk metadata.
    /// </summary>
    public List<ChunkMetadataV4> ChunkMetadataList { get; set; } = new();
    
    /// <summary>
    /// Deserializes a TimeseriesMetadata from a binary reader.
    /// </summary>
    public static TimeseriesMetadataV4 Deserialize(BinaryReader reader, Func<int> readUnsignedVarInt, Func<string> readVarIntString, Func<long> readLong, bool needChunkMetadata = true)
    {
        var metadata = new TimeseriesMetadataV4();
        metadata.TimeSeriesMetadataType = reader.ReadByte();
        metadata.MeasurementId = readVarIntString();
        metadata.DataType = (TsDataType)reader.ReadByte();
        // Java: ReadWriteForEncodingUtils.readUnsignedVarInt (no ZigZag)
        metadata.ChunkMetadataListDataSize = readUnsignedVarInt();
        metadata.Statistics = StatisticsV4.Deserialize(reader, metadata.DataType, readUnsignedVarInt, readLong);
        
        if (needChunkMetadata)
        {
            // Read chunk metadata list
            var startPos = reader.BaseStream.Position;
            while (reader.BaseStream.Position < startPos + metadata.ChunkMetadataListDataSize)
            {
                var chunkMeta = ChunkMetadataV4.Deserialize(reader, metadata, readUnsignedVarInt, readLong);
                metadata.ChunkMetadataList.Add(chunkMeta);
            }
        }
        else
        {
            // Skip chunk metadata
            reader.BaseStream.Position += metadata.ChunkMetadataListDataSize;
        }
        
        return metadata;
    }
    
    public override string ToString()
    {
        return $"TimeseriesMetadata{{MeasurementId='{MeasurementId}', DataType={DataType}, ChunkCount={ChunkMetadataList.Count}}}";
    }
}

/// <summary>
/// Statistics for TsFile v4 format.
/// </summary>
public class StatisticsV4
{
    public long Count { get; set; }
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public object? FirstValue { get; set; }
    public object? LastValue { get; set; }
    public object? SumValue { get; set; }
    
    public static StatisticsV4 Deserialize(BinaryReader reader, TsDataType dataType, Func<int> readUnsignedVarInt, Func<long> readLong)
    {
        var stats = new StatisticsV4();
        
        // Count is unsignedVarInt in Java (ReadWriteForEncodingUtils.readUnsignedVarInt)
        stats.Count = readUnsignedVarInt();
        
        // StartTime and EndTime
        stats.StartTime = readLong();
        stats.EndTime = readLong();
        
        // Type-specific values (must match Java *Statistics.serializeStats exactly)
        switch (dataType)
        {
            case TsDataType.Boolean:
                // Java BooleanStatistics: first(1) + last(1) + sum(8) = 10 bytes, NO min/max
                stats.FirstValue = reader.ReadBoolean();
                stats.LastValue = reader.ReadBoolean();
                stats.SumValue = readLong(); // sum of true values
                break;
                
            case TsDataType.Int32:
            case TsDataType.Date:
                // Java IntegerStatistics/DateStatistics: min(4) + max(4) + first(4) + last(4) + sum(8) = 24 bytes
                stats.MinValue = ReadInt32BigEndian(reader);
                stats.MaxValue = ReadInt32BigEndian(reader);
                stats.FirstValue = ReadInt32BigEndian(reader);
                stats.LastValue = ReadInt32BigEndian(reader);
                stats.SumValue = ReadDoubleBigEndian(reader);
                break;
                
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                // Java LongStatistics/TimestampStatistics: min(8) + max(8) + first(8) + last(8) + sum(8) = 40 bytes
                stats.MinValue = readLong();
                stats.MaxValue = readLong();
                stats.FirstValue = readLong();
                stats.LastValue = readLong();
                stats.SumValue = ReadDoubleBigEndian(reader);
                break;
                
            case TsDataType.Float:
                // Java FloatStatistics: min(4) + max(4) + first(4) + last(4) + sum(8) = 24 bytes
                stats.MinValue = ReadFloatBigEndian(reader);
                stats.MaxValue = ReadFloatBigEndian(reader);
                stats.FirstValue = ReadFloatBigEndian(reader);
                stats.LastValue = ReadFloatBigEndian(reader);
                stats.SumValue = ReadDoubleBigEndian(reader);
                break;
                
            case TsDataType.Double:
                // Java DoubleStatistics: min(8) + max(8) + first(8) + last(8) + sum(8) = 40 bytes
                stats.MinValue = ReadDoubleBigEndian(reader);
                stats.MaxValue = ReadDoubleBigEndian(reader);
                stats.FirstValue = ReadDoubleBigEndian(reader);
                stats.LastValue = ReadDoubleBigEndian(reader);
                stats.SumValue = ReadDoubleBigEndian(reader);
                break;
                
            case TsDataType.Text:
                // Java BinaryStatistics: first(4+len) + last(4+len), NO min/max
                // Uses Int32 (big-endian) length prefix (ReadWriteIOUtils.readBinary)
                {
                    var firstLen = ReadInt32BigEndian(reader);
                    stats.FirstValue = firstLen > 0 ? reader.ReadBytes(firstLen) : Array.Empty<byte>();
                    var lastLen = ReadInt32BigEndian(reader);
                    stats.LastValue = lastLen > 0 ? reader.ReadBytes(lastLen) : Array.Empty<byte>();
                    stats.SumValue = 0.0;
                }
                break;
                
            case TsDataType.String:
                // Java StringStatistics: first(4+len) + last(4+len) + min(4+len) + max(4+len)
                // Uses Int32 (big-endian) length prefix (ReadWriteIOUtils.readBinary)
                {
                    var firstLen = ReadInt32BigEndian(reader);
                    stats.FirstValue = firstLen > 0 ? reader.ReadBytes(firstLen) : Array.Empty<byte>();
                    var lastLen = ReadInt32BigEndian(reader);
                    stats.LastValue = lastLen > 0 ? reader.ReadBytes(lastLen) : Array.Empty<byte>();
                    var minLen = ReadInt32BigEndian(reader);
                    stats.MinValue = minLen > 0 ? reader.ReadBytes(minLen) : Array.Empty<byte>();
                    var maxLen = ReadInt32BigEndian(reader);
                    stats.MaxValue = maxLen > 0 ? reader.ReadBytes(maxLen) : Array.Empty<byte>();
                    stats.SumValue = 0.0;
                }
                break;
                
            case TsDataType.Blob:
                // Java BlobStatistics: 0 bytes (no stats)
                break;
                
            default:
                // Skip unknown types (e.g., VECTOR/TimeStatistics = 0 bytes)
                break;
        }
        
        return stats;
    }
    
    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }
    
    private static float ReadFloatBigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }
    
    private static double ReadDoubleBigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }
}

/// <summary>
/// Chunk metadata for TsFile v4 format.
/// </summary>
public class ChunkMetadataV4
{
    /// <summary>
    /// Gets the measurement ID.
    /// </summary>
    public string MeasurementId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets the offset of the chunk header in the file.
    /// </summary>
    public long OffsetOfChunkHeader { get; set; }
    
    /// <summary>
    /// Gets the data type.
    /// </summary>
    public TsDataType DataType { get; set; }
    
    /// <summary>
    /// Gets the mask byte (for time/value chunk identification).
    /// </summary>
    public byte Mask { get; set; }
    
    /// <summary>
    /// Gets the statistics for this chunk (optional for single-page chunks).
    /// </summary>
    public StatisticsV4? Statistics { get; set; }
    
    public static ChunkMetadataV4 Deserialize(BinaryReader reader, TimeseriesMetadataV4 timeseriesMetadata, Func<int> readUnsignedVarInt, Func<long> readLong)
    {
        var meta = new ChunkMetadataV4();
        meta.MeasurementId = timeseriesMetadata.MeasurementId;
        meta.DataType = timeseriesMetadata.DataType;
        
        // Read offset as big-endian Int64 (Java: ReadWriteIOUtils.readLong)
        meta.OffsetOfChunkHeader = readLong();
        
        // Check if this timeseries has multiple chunks (bit 0 of type, masking out time/value bits)
        // Java: (timeseriesMetadata.getTimeSeriesMetadataType() & 0x3F) != 0
        if ((timeseriesMetadata.TimeSeriesMetadataType & 0x3F) != 0)
        {
            // Has multiple chunks, each chunk metadata has its own statistics
            meta.Statistics = StatisticsV4.Deserialize(reader, meta.DataType, readUnsignedVarInt, readLong);
        }
        else
        {
            // Single chunk, no per-chunk statistics (use timeseries-level statistics)
            meta.Statistics = timeseriesMetadata.Statistics;
        }
        
        return meta;
    }
    
    public override string ToString()
    {
        return $"ChunkMetadata{{MeasurementId='{MeasurementId}', Offset={OffsetOfChunkHeader}, DataType={DataType}}}";
    }
}
