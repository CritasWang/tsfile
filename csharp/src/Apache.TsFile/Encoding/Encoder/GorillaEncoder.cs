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

namespace Apache.TsFile.Encoding.Encoder;

/// <summary>
/// Gorilla encoder - XOR-based compression for time-series floating-point data.
/// Lossless compression optimized for values that change slowly.
/// Based on Facebook's Gorilla compression algorithm.
/// </summary>
public class GorillaEncoder : IEncoder
{
    private readonly BitWriter _bitWriter = new();
    private long _previousValue;
    private int _previousLeadingZeros;
    private int _previousTrailingZeros;
    private bool _first = true;
    private readonly TsDataType _dataType;
    private readonly int _bitWidth;
    
    public GorillaEncoder(TsDataType dataType)
    {
        _dataType = dataType;
        _bitWidth = dataType switch
        {
            TsDataType.Float => 32,
            TsDataType.Double => 64,
            TsDataType.Int32 => 32,
            TsDataType.Int64 or TsDataType.Timestamp => 64,
            _ => 32
        };
    }
    
    public void Encode(bool value, MemoryStream stream)
    {
        throw new NotSupportedException("Gorilla encoding does not support boolean values");
    }
    
    public void Encode(int value, MemoryStream stream)
    {
        EncodeValue(value, 32);
    }
    
    public void Encode(long value, MemoryStream stream)
    {
        EncodeValue(value, 64);
    }
    
    public void Encode(float value, MemoryStream stream)
    {
        long longValue = BitConverter.SingleToInt32Bits(value);
        EncodeValue(longValue, 32);
    }
    
    public void Encode(double value, MemoryStream stream)
    {
        long longValue = BitConverter.DoubleToInt64Bits(value);
        EncodeValue(longValue, 64);
    }
    
    public void Encode(string value, MemoryStream stream)
    {
        throw new NotSupportedException("Gorilla encoding does not support string values");
    }
    
    public void Encode(byte[] value, MemoryStream stream)
    {
        throw new NotSupportedException("Gorilla encoding does not support byte array values");
    }
    
    public void Flush(MemoryStream stream)
    {
        // Java GorillaEncoderV2: write ending marker, then flush remaining bits
        // Int32/Float ending: Integer.MIN_VALUE (0x80000000)
        // Int64/Double ending: Long.MIN_VALUE (0x8000000000000000)
        if (_bitWidth == 32)
            EncodeValue(int.MinValue, 32);
        else
            EncodeValue(long.MinValue, 64);
        
        var bytes = _bitWriter.ToArray();
        
        // Java GorillaEncoderV2 writes raw bit-packed data with no length prefix
        stream.Write(bytes, 0, bytes.Length);
    }
    
    public int GetOneItemMaxSize()
    {
        return _bitWidth / 8 + 20; // Max size includes control bits
    }
    
    public long GetMaxByteSize()
    {
        return _bitWriter.GetBitCount() / 8 + 100;
    }
    
    private void EncodeValue(long value, int bitWidth)
    {
        if (_first)
        {
            // First value: write as-is
            _bitWriter.WriteBits(value, bitWidth);
            _previousValue = value;
            _previousLeadingZeros = int.MaxValue;
            _previousTrailingZeros = 0;
            _first = false;
            return;
        }
        
        // XOR with previous value
        long xor = value ^ _previousValue;
        _previousValue = value;
        
        if (xor == 0)
        {
            // Value is same as previous: write single '0' bit
            _bitWriter.WriteBit(0);
        }
        else
        {
            // Value changed: write '1' bit
            _bitWriter.WriteBit(1);
            
            // Count leading and trailing zeros in XOR
            int leadingZeros = CountLeadingZeros(xor, bitWidth);
            int trailingZeros = CountTrailingZeros(xor, bitWidth);
            
            // Java V2 bit widths: Int32=5/5, Int64=6/6
            int leadingZeroBits = bitWidth == 32 ? 5 : 6;
            int meaningfulBitsField = bitWidth == 32 ? 5 : 6;
            
            // Check if we can use previous block info
            if (leadingZeros >= _previousLeadingZeros && 
                trailingZeros >= _previousTrailingZeros)
            {
                // Use previous block: write '0' + meaningful bits
                _bitWriter.WriteBit(0);
                int significantBits = bitWidth - _previousLeadingZeros - _previousTrailingZeros;
                _bitWriter.WriteBits(xor >> _previousTrailingZeros, significantBits);
            }
            else
            {
                // New block: write '1' + leading zeros + (significantBits-1) + meaningful bits
                _bitWriter.WriteBit(1);
                
                int significantBits = bitWidth - leadingZeros - trailingZeros;
                _bitWriter.WriteBits(leadingZeros, leadingZeroBits);
                // Java stores (significantBits - 1) to allow encoding the full bit width
                _bitWriter.WriteBits(significantBits - 1, meaningfulBitsField);
                _bitWriter.WriteBits(xor >> trailingZeros, significantBits);
                
                _previousLeadingZeros = leadingZeros;
                _previousTrailingZeros = trailingZeros;
            }
        }
    }
    
    private static int CountLeadingZeros(long value, int bitWidth)
    {
        if (value == 0) return bitWidth;
        
        int count = 0;
        long mask = 1L << (bitWidth - 1);
        
        while ((value & mask) == 0 && count < bitWidth)
        {
            count++;
            mask >>= 1;
        }
        
        return count;
    }
    
    private static int CountTrailingZeros(long value, int bitWidth)
    {
        if (value == 0) return bitWidth;
        
        int count = 0;
        while ((value & 1) == 0 && count < bitWidth)
        {
            count++;
            value >>= 1;
        }
        
        return count;
    }
    
    private static void WriteInt(Stream stream, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        stream.Write(bytes, 0, 4);
    }
}
