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
/// Gorilla V1 encoder - legacy version of Gorilla encoding for floats/doubles.
/// Based on Facebook's Gorilla compression algorithm V1.
/// </summary>
public class GorillaV1Encoder : IEncoder
{
    private readonly TsDataType _dataType;
    private bool _flag = false;
    private int _leadingZeroNum;
    private int _tailingZeroNum;
    private byte _buffer;
    private int _numberLeftInBuffer;
    private readonly List<byte> _bytes = new();
    
    private long _storedValue;
    private readonly int _bitWidth;
    
    public GorillaV1Encoder(TsDataType dataType)
    {
        _dataType = dataType;
        _bitWidth = dataType switch
        {
            TsDataType.Float => 32,
            TsDataType.Double => 64,
            _ => throw new NotSupportedException($"GorillaV1 encoding does not support {dataType}")
        };
    }
    
    public void Encode(bool value, MemoryStream stream)
    {
        throw new NotSupportedException("GorillaV1 encoding does not support boolean values");
    }
    
    public void Encode(int value, MemoryStream stream)
    {
        throw new NotSupportedException("GorillaV1 encoding does not support int values");
    }
    
    public void Encode(long value, MemoryStream stream)
    {
        throw new NotSupportedException("GorillaV1 encoding does not support long values");
    }
    
    public void Encode(float value, MemoryStream stream)
    {
        int intValue = BitConverter.SingleToInt32Bits(value);
        EncodeFloat(intValue);
    }
    
    public void Encode(double value, MemoryStream stream)
    {
        long longValue = BitConverter.DoubleToInt64Bits(value);
        EncodeDouble(longValue);
    }
    
    public void Encode(string value, MemoryStream stream)
    {
        throw new NotSupportedException("GorillaV1 encoding does not support string values");
    }
    
    public void Encode(byte[] value, MemoryStream stream)
    {
        throw new NotSupportedException("GorillaV1 encoding does not support byte array values");
    }
    
    public void Flush(MemoryStream stream)
    {
        // Java V1: write NaN as ending marker, then flush
        if (_bitWidth == 32)
            EncodeFloat(BitConverter.SingleToInt32Bits(float.NaN));
        else
            EncodeDouble(BitConverter.DoubleToInt64Bits(double.NaN));
        ClearBuffer();
        stream.Write(_bytes.ToArray());
        Reset();
    }
    
    public int GetOneItemMaxSize()
    {
        return _bitWidth / 8 + 20;
    }
    
    public long GetMaxByteSize()
    {
        return _bytes.Count + 100;
    }
    
    /// <summary>
    /// Java V1 float encoding: first value as 4 bytes little-endian,
    /// then XOR-compressed bits. Uses LEADING_ZERO_BITS=5, FLOAT_VALUE_LENGTH=6.
    /// Leading/trailing zeros tracked from the VALUE, not XOR.
    /// </summary>
    private void EncodeFloat(int value)
    {
        if (!_flag)
        {
            _flag = true;
            _storedValue = value;
            _leadingZeroNum = CountLeadingZeros32(value);
            _tailingZeroNum = CountTrailingZeros32(value);
            // Write first value as 4 bytes little-endian
            _bytes.Add((byte)(value & 0xFF));
            _bytes.Add((byte)((value >> 8) & 0xFF));
            _bytes.Add((byte)((value >> 16) & 0xFF));
            _bytes.Add((byte)((value >> 24) & 0xFF));
            return;
        }
        
        int preValue = (int)_storedValue;
        int tmp = value ^ preValue;
        if (tmp == 0)
        {
            WriteBit(false);
        }
        else
        {
            int leadingZeroNumTmp = CountLeadingZeros32(tmp);
            int tailingZeroNumTmp = CountTrailingZeros32(tmp);
            if (leadingZeroNumTmp >= _leadingZeroNum && tailingZeroNumTmp >= _tailingZeroNum)
            {
                // case '10'
                WriteBit(true);
                WriteBit(false);
                WriteBitsRange(tmp, 32 - 1 - _leadingZeroNum, _tailingZeroNum);
            }
            else
            {
                // case '11'
                WriteBit(true);
                WriteBit(true);
                WriteIntBits(leadingZeroNumTmp, 5); // LEADING_ZERO_BITS_LENGTH_32BIT
                int significantBits = 32 - leadingZeroNumTmp - tailingZeroNumTmp;
                WriteIntBits(significantBits, 6); // FLOAT_VALUE_LENGTH
                WriteBitsRange(tmp, 32 - 1 - leadingZeroNumTmp, tailingZeroNumTmp);
            }
        }
        _storedValue = value;
        _leadingZeroNum = CountLeadingZeros32(value);
        _tailingZeroNum = CountTrailingZeros32(value);
    }
    
    /// <summary>
    /// Java V1 double encoding: first value as 8 bytes little-endian,
    /// then XOR-compressed bits. Uses LEADING_ZERO_BITS=6, DOUBLE_VALUE_LENGTH=7.
    /// </summary>
    private void EncodeDouble(long value)
    {
        if (!_flag)
        {
            _flag = true;
            _storedValue = value;
            _leadingZeroNum = CountLeadingZeros64(value);
            _tailingZeroNum = CountTrailingZeros64(value);
            // Write first value as 8 bytes little-endian
            for (int i = 0; i < 8; i++)
                _bytes.Add((byte)((value >> (i * 8)) & 0xFF));
            return;
        }
        
        long preValue = _storedValue;
        long tmp = value ^ preValue;
        if (tmp == 0)
        {
            WriteBit(false);
        }
        else
        {
            int leadingZeroNumTmp = CountLeadingZeros64(tmp);
            int tailingZeroNumTmp = CountTrailingZeros64(tmp);
            if (leadingZeroNumTmp >= _leadingZeroNum && tailingZeroNumTmp >= _tailingZeroNum)
            {
                // case '10'
                WriteBit(true);
                WriteBit(false);
                WriteLongBitsRange(tmp, 64 - 1 - _leadingZeroNum, _tailingZeroNum);
            }
            else
            {
                // case '11'
                WriteBit(true);
                WriteBit(true);
                WriteIntBits(leadingZeroNumTmp, 6); // LEADING_ZERO_BITS_LENGTH_64BIT
                int significantBits = 64 - leadingZeroNumTmp - tailingZeroNumTmp;
                WriteIntBits(significantBits, 7); // DOUBLE_VALUE_LENGTH
                WriteLongBitsRange(tmp, 64 - 1 - leadingZeroNumTmp, tailingZeroNumTmp);
            }
        }
        _storedValue = value;
        _leadingZeroNum = CountLeadingZeros64(value);
        _tailingZeroNum = CountTrailingZeros64(value);
    }
    
    /// <summary>Write bits from position start down to end (inclusive).</summary>
    private void WriteBitsRange(int num, int start, int end)
    {
        for (int i = start; i >= end; i--)
        {
            WriteBit((num & (1 << i)) != 0);
        }
    }
    
    /// <summary>Write bits from position start down to end (inclusive).</summary>
    private void WriteLongBitsRange(long num, int start, int end)
    {
        for (int i = start; i >= end; i--)
        {
            WriteBit((num & (1L << i)) != 0);
        }
    }
    
    private void WriteBit(bool bit)
    {
        _buffer <<= 1;
        if (bit)
        {
            _buffer |= 1;
        }
        
        _numberLeftInBuffer++;
        if (_numberLeftInBuffer == 8)
        {
            ClearBuffer();
        }
    }
    
    private void WriteIntBits(int value, int bits)
    {
        for (int i = bits - 1; i >= 0; i--)
        {
            WriteBit(((value >> i) & 1) != 0);
        }
    }
    
    private void WriteLongBits(long value, int bits)
    {
        for (int i = bits - 1; i >= 0; i--)
        {
            WriteBit(((value >> i) & 1) != 0);
        }
    }
    
    private void ClearBuffer()
    {
        if (_numberLeftInBuffer == 0)
        {
            return;
        }
        if (_numberLeftInBuffer > 0)
        {
            _buffer <<= (8 - _numberLeftInBuffer);
        }
        _bytes.Add(_buffer);
        _numberLeftInBuffer = 0;
        _buffer = 0;
    }
    
    private void Reset()
    {
        _flag = false;
        _numberLeftInBuffer = 0;
        _buffer = 0;
        _bytes.Clear();
    }
    
    private static int CountLeadingZeros32(int value)
    {
        if (value == 0) return 32;
        int n = 0;
        uint v = (uint)value;
        if (v <= 0x0000FFFF) { n += 16; v <<= 16; }
        if (v <= 0x00FFFFFF) { n += 8; v <<= 8; }
        if (v <= 0x0FFFFFFF) { n += 4; v <<= 4; }
        if (v <= 0x3FFFFFFF) { n += 2; v <<= 2; }
        if (v <= 0x7FFFFFFF) { n += 1; }
        return n;
    }
    
    private static int CountTrailingZeros32(int value)
    {
        if (value == 0) return 32;
        int n = 0;
        uint v = (uint)value;
        if ((v & 0x0000FFFF) == 0) { n += 16; v >>= 16; }
        if ((v & 0x000000FF) == 0) { n += 8; v >>= 8; }
        if ((v & 0x0000000F) == 0) { n += 4; v >>= 4; }
        if ((v & 0x00000003) == 0) { n += 2; v >>= 2; }
        if ((v & 0x00000001) == 0) { n += 1; }
        return n;
    }
    
    private static int CountLeadingZeros64(long value)
    {
        if (value == 0) return 64;
        int n = 0;
        ulong v = (ulong)value;
        if (v <= 0x00000000FFFFFFFF) { n += 32; v <<= 32; }
        if (v <= 0x0000FFFFFFFFFFFF) { n += 16; v <<= 16; }
        if (v <= 0x00FFFFFFFFFFFFFF) { n += 8; v <<= 8; }
        if (v <= 0x0FFFFFFFFFFFFFFF) { n += 4; v <<= 4; }
        if (v <= 0x3FFFFFFFFFFFFFFF) { n += 2; v <<= 2; }
        if (v <= 0x7FFFFFFFFFFFFFFF) { n += 1; }
        return n;
    }
    
    private static int CountTrailingZeros64(long value)
    {
        if (value == 0) return 64;
        int n = 0;
        ulong v = (ulong)value;
        if ((v & 0x00000000FFFFFFFF) == 0) { n += 32; v >>= 32; }
        if ((v & 0x000000000000FFFF) == 0) { n += 16; v >>= 16; }
        if ((v & 0x00000000000000FF) == 0) { n += 8; v >>= 8; }
        if ((v & 0x000000000000000F) == 0) { n += 4; v >>= 4; }
        if ((v & 0x0000000000000003) == 0) { n += 2; v >>= 2; }
        if ((v & 0x0000000000000001) == 0) { n += 1; }
        return n;
    }
}
