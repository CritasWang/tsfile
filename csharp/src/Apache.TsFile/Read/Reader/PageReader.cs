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

using Apache.TsFile.Compress;
using Apache.TsFile.Encoding;
using Apache.TsFile.Enums;
using Apache.TsFile.Read.Common;
using Apache.TsFile.Read.Filter;

namespace Apache.TsFile.Read.Reader;

/// <summary>
/// Page reader implementation for reading data from a single page.
/// </summary>
public class PageReader : IPageReader
{
    private readonly TsDataType _dataType;
    private readonly IDecoder _valueDecoder;
    private readonly IDecoder _timeDecoder;
    private readonly byte[] _pageData;
    private readonly PageStatistics? _statistics;
    
    private Filter.Filter? _recordFilter;
    private PaginationController _paginationController = PaginationController.Unlimited;
    private IReadOnlyList<TimeRange>? _deleteIntervalList;
    private int _deleteCursor;
    private bool _isModified;
    
    /// <summary>
    /// Creates a new page reader.
    /// </summary>
    /// <param name="pageData">The uncompressed page data.</param>
    /// <param name="dataType">The data type of values in this page.</param>
    /// <param name="valueDecoder">The decoder for value data.</param>
    /// <param name="timeDecoder">The decoder for time data.</param>
    /// <param name="statistics">Optional page statistics.</param>
    /// <param name="recordFilter">Optional record filter.</param>
    public PageReader(
        byte[] pageData,
        TsDataType dataType,
        IDecoder valueDecoder,
        IDecoder timeDecoder,
        PageStatistics? statistics = null,
        Filter.Filter? recordFilter = null)
    {
        _pageData = pageData;
        _dataType = dataType;
        _valueDecoder = valueDecoder;
        _timeDecoder = timeDecoder;
        _statistics = statistics;
        _recordFilter = recordFilter;
    }
    
    /// <inheritdoc />
    public IStatistics? Statistics => _statistics;
    
    /// <inheritdoc />
    public bool IsModified => _isModified;
    
    /// <inheritdoc />
    public void SetModified(bool modified) => _isModified = modified;
    
    /// <inheritdoc />
    public void AddRecordFilter(Filter.Filter filter)
    {
        _recordFilter = FilterFactory.And(_recordFilter, filter);
    }
    
    /// <inheritdoc />
    public void SetLimitOffset(PaginationController paginationController)
    {
        _paginationController = paginationController;
    }
    
    /// <summary>
    /// Sets the delete interval list for this page.
    /// </summary>
    public void SetDeleteIntervalList(IReadOnlyList<TimeRange> deleteIntervalList)
    {
        _deleteIntervalList = deleteIntervalList;
    }
    
    /// <inheritdoc />
    public void InitTsBlockBuilder(IReadOnlyList<TsDataType> dataTypes)
    {
        // Implementation for TsBlock builder initialization
    }
    
    /// <inheritdoc />
    public BatchData GetAllSatisfiedPageData(bool ascending = true)
    {
        var batchData = BatchData.Create(_dataType, ascending);
        bool allSatisfy = _recordFilter == null || (_statistics != null && _recordFilter.AllSatisfy(_statistics));
        
        int offset = 0;
        
        // First, read the time buffer length
        if (_pageData.Length < 4) return batchData.Flip();
        
        int timeBufferLength = ReadVarInt(_pageData, ref offset);
        
        // Split page data into time and value buffers
        int timeBufferStart = offset;
        int valueBufferStart = offset + timeBufferLength;
        
        if (valueBufferStart > _pageData.Length) return batchData.Flip();
        
        int timeOffset = timeBufferStart;
        int valueOffset = valueBufferStart;
        
        while (timeOffset < valueBufferStart && _timeDecoder.HasNext(_pageData, timeOffset))
        {
            long timestamp = _timeDecoder.ReadLong(_pageData, ref timeOffset);
            
            switch (_dataType)
            {
                case TsDataType.Boolean:
                    {
                        bool value = _valueDecoder.ReadBoolean(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyBoolean(timestamp, value)))
                        {
                            if (HandlePagination())
                                batchData.PutBoolean(timestamp, value);
                        }
                    }
                    break;
                case TsDataType.Int32:
                case TsDataType.Date:
                    {
                        int value = _valueDecoder.ReadInt(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyInteger(timestamp, value)))
                        {
                            if (HandlePagination())
                                batchData.PutInt(timestamp, value);
                        }
                    }
                    break;
                case TsDataType.Int64:
                case TsDataType.Timestamp:
                    {
                        long value = _valueDecoder.ReadLong(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyLong(timestamp, value)))
                        {
                            if (HandlePagination())
                                batchData.PutLong(timestamp, value);
                        }
                    }
                    break;
                case TsDataType.Float:
                    {
                        float value = _valueDecoder.ReadFloat(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyFloat(timestamp, value)))
                        {
                            if (HandlePagination())
                                batchData.PutFloat(timestamp, value);
                        }
                    }
                    break;
                case TsDataType.Double:
                    {
                        double value = _valueDecoder.ReadDouble(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyDouble(timestamp, value)))
                        {
                            if (HandlePagination())
                                batchData.PutDouble(timestamp, value);
                        }
                    }
                    break;
                case TsDataType.Text:
                case TsDataType.String:
                    {
                        string? value = _valueDecoder.ReadString(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyString(timestamp, value)))
                        {
                            if (HandlePagination())
                                batchData.PutString(timestamp, value);
                        }
                    }
                    break;
                case TsDataType.Blob:
                    {
                        byte[]? value = _valueDecoder.ReadBytes(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyBinary(timestamp, value)))
                        {
                            if (HandlePagination())
                                batchData.PutBinary(timestamp, value);
                        }
                    }
                    break;
            }
            
            if (!_paginationController.HasCurLimit)
                break;
        }
        
        return batchData.Flip();
    }
    
    /// <inheritdoc />
    public TsBlock GetAllSatisfiedData()
    {
        var builder = new TsBlockBuilder(new[] { _dataType });
        var timeBuilder = builder.GetTimeColumnBuilder();
        var valueBuilder = builder.GetColumnBuilder(0);
        
        bool allSatisfy = _recordFilter == null || (_statistics != null && _recordFilter.AllSatisfy(_statistics));
        
        int offset = 0;
        
        if (_pageData.Length < 4) return builder.Build();
        
        int timeBufferLength = ReadVarInt(_pageData, ref offset);
        int timeBufferStart = offset;
        int valueBufferStart = offset + timeBufferLength;
        
        if (valueBufferStart > _pageData.Length) return builder.Build();
        
        int timeOffset = timeBufferStart;
        int valueOffset = valueBufferStart;
        
        while (timeOffset < valueBufferStart && _timeDecoder.HasNext(_pageData, timeOffset))
        {
            long timestamp = _timeDecoder.ReadLong(_pageData, ref timeOffset);
            
            switch (_dataType)
            {
                case TsDataType.Boolean:
                    {
                        bool value = _valueDecoder.ReadBoolean(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyBoolean(timestamp, value)))
                        {
                            if (HandlePagination())
                            {
                                timeBuilder.WriteLong(timestamp);
                                valueBuilder.WriteBoolean(value);
                                builder.DeclarePosition();
                            }
                        }
                    }
                    break;
                case TsDataType.Int32:
                case TsDataType.Date:
                    {
                        int value = _valueDecoder.ReadInt(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyInteger(timestamp, value)))
                        {
                            if (HandlePagination())
                            {
                                timeBuilder.WriteLong(timestamp);
                                valueBuilder.WriteInt(value);
                                builder.DeclarePosition();
                            }
                        }
                    }
                    break;
                case TsDataType.Int64:
                case TsDataType.Timestamp:
                    {
                        long value = _valueDecoder.ReadLong(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyLong(timestamp, value)))
                        {
                            if (HandlePagination())
                            {
                                timeBuilder.WriteLong(timestamp);
                                valueBuilder.WriteLong(value);
                                builder.DeclarePosition();
                            }
                        }
                    }
                    break;
                case TsDataType.Float:
                    {
                        float value = _valueDecoder.ReadFloat(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyFloat(timestamp, value)))
                        {
                            if (HandlePagination())
                            {
                                timeBuilder.WriteLong(timestamp);
                                valueBuilder.WriteFloat(value);
                                builder.DeclarePosition();
                            }
                        }
                    }
                    break;
                case TsDataType.Double:
                    {
                        double value = _valueDecoder.ReadDouble(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyDouble(timestamp, value)))
                        {
                            if (HandlePagination())
                            {
                                timeBuilder.WriteLong(timestamp);
                                valueBuilder.WriteDouble(value);
                                builder.DeclarePosition();
                            }
                        }
                    }
                    break;
                case TsDataType.Text:
                case TsDataType.String:
                case TsDataType.Blob:
                    {
                        byte[]? value = _valueDecoder.ReadBytes(_pageData, ref valueOffset);
                        if (!IsDeleted(timestamp) && (allSatisfy || _recordFilter!.SatisfyBinary(timestamp, value)))
                        {
                            if (HandlePagination())
                            {
                                timeBuilder.WriteLong(timestamp);
                                valueBuilder.WriteBinary(value);
                                builder.DeclarePosition();
                            }
                        }
                    }
                    break;
            }
            
            if (!_paginationController.HasCurLimit)
                break;
        }
        
        return builder.Build();
    }
    
    private bool HandlePagination()
    {
        if (_paginationController.HasCurOffset)
        {
            _paginationController.ConsumeOffset();
            return false;
        }
        if (_paginationController.HasCurLimit)
        {
            _paginationController.ConsumeLimit();
            return true;
        }
        return false;
    }
    
    private bool IsDeleted(long timestamp)
    {
        if (_deleteIntervalList == null) return false;
        
        while (_deleteCursor < _deleteIntervalList.Count)
        {
            var range = _deleteIntervalList[_deleteCursor];
            if (range.Contains(timestamp))
                return true;
            if (range.Max < timestamp)
                _deleteCursor++;
            else
                return false;
        }
        return false;
    }
    
    private static int ReadVarInt(byte[] data, ref int offset)
    {
        int result = 0;
        int shift = 0;
        byte b;
        
        do
        {
            if (offset >= data.Length) return 0;
            b = data[offset++];
            result |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        
        return result;
    }
}

/// <summary>
/// Statistics for a page.
/// </summary>
public class PageStatistics : IStatistics
{
    public long StartTime { get; set; }
    public long EndTime { get; set; }
    public long Count { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public object? FirstValue { get; set; }
    public object? LastValue { get; set; }
    public double Sum { get; set; }
}
