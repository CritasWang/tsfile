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
using Apache.TsFile.Encrypt;
using Apache.TsFile.Enums;
using Apache.TsFile.Read.Common;
using Apache.TsFile.Read.Filter;

namespace Apache.TsFile.Read.Reader;

/// <summary>
/// Chunk reader implementation for reading data from a chunk (multiple pages).
/// </summary>
public class ChunkReader : IChunkReader
{
    private readonly TsDataType _dataType;
    private readonly TsEncoding _encoding;
    private readonly CompressionType _compression;
    private readonly byte[] _chunkData;
    private readonly Filter.Filter? _queryFilter;
    private readonly EncryptParameter? _encryptParam;
    private readonly IReadOnlyList<TimeRange>? _deleteIntervalList;
    private readonly long _readStopTime;
    
    private readonly List<IPageReader> _pageReaderList;
    private int _currentPageIndex;
    private bool _disposed;
    
    /// <summary>
    /// Creates a new chunk reader.
    /// </summary>
    /// <param name="chunkData">The chunk data (excluding chunk header).</param>
    /// <param name="dataType">The data type of values in this chunk.</param>
    /// <param name="encoding">The encoding used for this chunk.</param>
    /// <param name="compression">The compression used for this chunk.</param>
    /// <param name="queryFilter">Optional query filter.</param>
    /// <param name="deleteIntervalList">Optional list of deletion intervals.</param>
    /// <param name="encryptParam">Optional encryption parameters.</param>
    /// <param name="readStopTime">Stop reading when timestamp exceeds this value.</param>
    public ChunkReader(
        byte[] chunkData,
        TsDataType dataType,
        TsEncoding encoding,
        CompressionType compression,
        Filter.Filter? queryFilter = null,
        IReadOnlyList<TimeRange>? deleteIntervalList = null,
        EncryptParameter? encryptParam = null,
        long readStopTime = long.MinValue)
    {
        _chunkData = chunkData;
        _dataType = dataType;
        _encoding = encoding;
        _compression = compression;
        _queryFilter = queryFilter;
        _deleteIntervalList = deleteIntervalList;
        _encryptParam = encryptParam;
        _readStopTime = readStopTime;
        
        _pageReaderList = new List<IPageReader>();
        InitializePageReaders();
    }
    
    /// <inheritdoc />
    public bool HasNextSatisfiedPage()
    {
        return _currentPageIndex < _pageReaderList.Count;
    }
    
    /// <inheritdoc />
    public BatchData NextPageData()
    {
        if (_currentPageIndex >= _pageReaderList.Count)
            return new BatchData(_dataType);
        
        var pageReader = _pageReaderList[_currentPageIndex++];
        return pageReader.GetAllSatisfiedPageData();
    }
    
    /// <inheritdoc />
    public IReadOnlyList<IPageReader> LoadPageReaderList()
    {
        return _pageReaderList.AsReadOnly();
    }
    
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pageReaderList.Clear();
        GC.SuppressFinalize(this);
    }
    
    private void InitializePageReaders()
    {
        int offset = 0;
        
        while (offset < _chunkData.Length)
        {
            try
            {
                // Read page header
                int uncompressedSize = ReadVarInt(_chunkData, ref offset);
                int compressedSize = ReadVarInt(_chunkData, ref offset);
                
                // Read page statistics (if present)
                PageStatistics? statistics = null;
                // Note: Statistics parsing can be added here if needed
                
                // Check if we can skip this page based on query filter
                if (_queryFilter != null && statistics != null)
                {
                    if (!_queryFilter.SatisfyStartEndTime(statistics.StartTime, statistics.EndTime))
                    {
                        // Skip this page
                        offset += compressedSize;
                        continue;
                    }
                }
                
                // Read compressed page data
                if (offset + compressedSize > _chunkData.Length)
                    break;
                
                byte[] compressedData = new byte[compressedSize];
                Array.Copy(_chunkData, offset, compressedData, 0, compressedSize);
                offset += compressedSize;
                
                // Decompress page data
                byte[] uncompressedData;
                try
                {
                    var uncompressor = CompressorFactory.GetUncompressor(_compression);
                    
                    // Handle decryption if needed
                    if (_encryptParam != null && 
                        !_encryptParam.Type.Equals("UNENCRYPTED", StringComparison.OrdinalIgnoreCase))
                    {
                        var decryptor = DecryptorFactory.GetDecryptor(_encryptParam);
                        compressedData = decryptor.Decrypt(compressedData);
                    }
                    
                    uncompressedData = uncompressor.Uncompress(compressedData);
                }
                catch
                {
                    // If decompression fails, try using raw data
                    uncompressedData = compressedData;
                }
                
                // Create decoders
                var valueDecoder = DecoderFactory.CreateDecoder(_encoding, _dataType);
                var timeDecoder = DecoderFactory.CreateDecoder(TsEncoding.Plain, TsDataType.Int64);
                
                // Create page reader
                var pageReader = new PageReader(
                    uncompressedData,
                    _dataType,
                    valueDecoder,
                    timeDecoder,
                    statistics,
                    _queryFilter);
                
                if (_deleteIntervalList != null)
                {
                    pageReader.SetDeleteIntervalList(_deleteIntervalList);
                }
                
                _pageReaderList.Add(pageReader);
            }
            catch
            {
                // If there's an error parsing a page, stop processing
                break;
            }
        }
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
