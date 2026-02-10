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

namespace Apache.TsFile.Encoding.Decoder;

/// <summary>
/// RLE (Run-Length Encoding) decoder using hybrid RLE + bit-packing approach.
/// </summary>
public class RleDecoder : IDecoder
{
    private const int BitPackedGroupSize = 8;
    
    private byte[]? _buffer;
    private int _bufferOffset;
    private int _bitWidth;
    private readonly Queue<int> _intQueue = new();
    private readonly Queue<long> _longQueue = new();
    private readonly TsDataType _dataType;
    private bool _isLong;
    
    public RleDecoder(TsDataType dataType)
    {
        _dataType = dataType;
        _isLong = dataType == TsDataType.Int64 || dataType == TsDataType.Double || dataType == TsDataType.Timestamp;
    }
    
    public bool ReadBoolean(byte[] buffer, ref int offset)
    {
        return ReadInt(buffer, ref offset) != 0;
    }
    
    public int ReadInt(byte[] buffer, ref int offset)
    {
        EnsureData(buffer, ref offset);
        return _intQueue.Dequeue();
    }
    
    public long ReadLong(byte[] buffer, ref int offset)
    {
        EnsureDataLong(buffer, ref offset);
        return _longQueue.Dequeue();
    }
    
    public float ReadFloat(byte[] buffer, ref int offset)
    {
        return BitConverter.Int32BitsToSingle(ReadInt(buffer, ref offset));
    }
    
    public double ReadDouble(byte[] buffer, ref int offset)
    {
        return BitConverter.Int64BitsToDouble(ReadLong(buffer, ref offset));
    }
    
    public string ReadString(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("RLE decoder does not support string values");
    }
    
    public byte[] ReadBytes(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("RLE decoder does not support byte array values");
    }
    
    public bool HasNext(byte[] buffer, int offset)
    {
        if (_isLong)
            return _longQueue.Count > 0 || (_buffer != null && _bufferOffset < _buffer.Length) || offset < buffer.Length;
        return _intQueue.Count > 0 || (_buffer != null && _bufferOffset < _buffer.Length) || offset < buffer.Length;
    }
    
    public void Reset()
    {
        _buffer = null;
        _bufferOffset = 0;
        _bitWidth = 0;
        _intQueue.Clear();
        _longQueue.Clear();
    }
    
    private void EnsureData(byte[] buffer, ref int offset)
    {
        if (_intQueue.Count > 0) return;
        
        if (_buffer == null || _bufferOffset >= _buffer.Length)
        {
            // Java format: length is unsignedVarInt, then bitWidth byte, then encoded data
            int length = ReadUnsignedVarInt(buffer, ref offset);
            _buffer = new byte[length];
            Array.Copy(buffer, offset, _buffer, 0, length);
            offset += length;
            _bufferOffset = 0;
            _bitWidth = _buffer[_bufferOffset++];
        }
        
        DecodeNextRun();
    }
    
    private void EnsureDataLong(byte[] buffer, ref int offset)
    {
        if (_longQueue.Count > 0) return;
        
        if (_buffer == null || _bufferOffset >= _buffer.Length)
        {
            int length = ReadUnsignedVarInt(buffer, ref offset);
            _buffer = new byte[length];
            Array.Copy(buffer, offset, _buffer, 0, length);
            offset += length;
            _bufferOffset = 0;
            _bitWidth = _buffer[_bufferOffset++];
        }
        
        DecodeNextRunLong();
    }
    
    private void DecodeNextRun()
    {
        if (_buffer == null || _bufferOffset >= _buffer.Length) return;
        
        int header = ReadVarInt(_buffer, ref _bufferOffset);
        bool isRle = (header & 1) == 0;
        
        if (isRle)
        {
            // RLE run
            int count = header >> 1;
            int value = ReadPaddedInt(_buffer, ref _bufferOffset, _bitWidth);
            for (int i = 0; i < count; i++)
            {
                _intQueue.Enqueue(value);
            }
        }
        else
        {
            // Bit-packed run
            int groupCount = header >> 1;
            int lastNum = _buffer[_bufferOffset++];
            
            for (int g = 0; g < groupCount; g++)
            {
                int count = (g == groupCount - 1) ? lastNum : BitPackedGroupSize;
                UnpackInts(_buffer, ref _bufferOffset, _bitWidth, count);
            }
        }
    }
    
    private void DecodeNextRunLong()
    {
        if (_buffer == null || _bufferOffset >= _buffer.Length) return;
        
        int header = ReadVarInt(_buffer, ref _bufferOffset);
        bool isRle = (header & 1) == 0;
        
        if (isRle)
        {
            int count = header >> 1;
            long value = ReadPaddedLong(_buffer, ref _bufferOffset, _bitWidth);
            for (int i = 0; i < count; i++)
            {
                _longQueue.Enqueue(value);
            }
        }
        else
        {
            int groupCount = header >> 1;
            int lastNum = _buffer[_bufferOffset++];
            
            for (int g = 0; g < groupCount; g++)
            {
                int count = (g == groupCount - 1) ? lastNum : BitPackedGroupSize;
                UnpackLongs(_buffer, ref _bufferOffset, _bitWidth, count);
            }
        }
    }
    
    private void UnpackInts(byte[] buffer, ref int offset, int bitWidth, int count)
    {
        // Java IntPacker format: big-endian bit packing
        // Values are packed MSB-first into a 32-bit buffer, written as big-endian bytes
        // Each group of 8 values occupies exactly bitWidth bytes
        if (bitWidth == 0)
        {
            for (int i = 0; i < count; i++)
                _intQueue.Enqueue(0);
            return;
        }
        
        int byteIdx = offset;
        long buf = 0;
        int totalBits = 0;
        
        for (int i = 0; i < count; i++)
        {
            while (totalBits < bitWidth)
            {
                buf = (buf << 8) | (buffer[byteIdx] & 0xFF);
                byteIdx++;
                totalBits += 8;
            }
            
            int value = (int)(buf >> (totalBits - bitWidth));
            totalBits -= bitWidth;
            buf = buf & ((1L << totalBits) - 1);
            
            _intQueue.Enqueue(value);
        }
        
        // Java always packs 8 values into bitWidth bytes, even for partial groups
        offset += bitWidth;
    }
    
    private void UnpackLongs(byte[] buffer, ref int offset, int bitWidth, int count)
    {
        // Java LongPacker format: big-endian bit packing, same as IntPacker but for 64-bit values
        // Each group of 8 values occupies exactly bitWidth bytes
        if (bitWidth == 0)
        {
            for (int i = 0; i < count; i++)
                _longQueue.Enqueue(0);
            return;
        }
        
        int byteIdx = offset;
        // Use BigInteger-like accumulation for wide bit widths
        long buf = 0;
        int totalBits = 0;
        
        for (int i = 0; i < count; i++)
        {
            while (totalBits < bitWidth)
            {
                buf = (buf << 8) | (buffer[byteIdx] & 0xFF);
                byteIdx++;
                totalBits += 8;
            }
            
            long value = (buf >> (totalBits - bitWidth));
            totalBits -= bitWidth;
            buf = buf & ((1L << totalBits) - 1);
            
            _longQueue.Enqueue(value);
        }
        
        // Java always packs 8 values into bitWidth bytes, even for partial groups
        offset += bitWidth;
    }
    
    private static int ReadUnsignedVarInt(byte[] buffer, ref int offset)
    {
        int value = 0;
        int shift = 0;
        while (true)
        {
            byte b = buffer[offset++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return value;
    }
    
    private static int ReadVarInt(byte[] buffer, ref int offset)
    {
        int result = 0;
        int shift = 0;
        
        while (true)
        {
            byte b = buffer[offset++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        
        return result;
    }
    
    private static int ReadPaddedInt(byte[] buffer, ref int offset, int bitWidth)
    {
        // Java writes RLE repeated value in little-endian byte order
        int byteWidth = (bitWidth + 7) / 8;
        int value = 0;
        for (int i = 0; i < byteWidth; i++)
        {
            value |= (buffer[offset + i] & 0xFF) << (i * 8);
        }
        offset += byteWidth;
        return value;
    }
    
    private static long ReadPaddedLong(byte[] buffer, ref int offset, int bitWidth)
    {
        // Java writeLongLittleEndianPaddedOnBitWidth uses BytesUtils.longToBytes which is BIG-ENDIAN
        // despite the method name suggesting little-endian
        int byteWidth = (bitWidth + 7) / 8;
        long value = 0;
        for (int i = 0; i < byteWidth; i++)
        {
            value = (value << 8) | (buffer[offset + i] & 0xFF);
        }
        offset += byteWidth;
        return value;
    }
}
