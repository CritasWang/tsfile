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
/// Decoder for Gorilla V1 encoded data (floats and doubles).
/// </summary>
public class GorillaV1Decoder : IDecoder
{
    private readonly TsDataType _dataType;
    private bool _flag = false;
    private int _leadingZeroNum;
    private int _tailingZeroNum;
    private bool _isEnd;
    private int _bitBuffer = -1;
    private int _numberLeftInBuffer;
    private long _storedValue;
    private readonly int _bitWidth;
    private byte[] _sourceBuffer = Array.Empty<byte>();
    private int _sourceOffset;
    
    private readonly Queue<float> _floatQueue = new();
    private readonly Queue<double> _doubleQueue = new();
    
    public GorillaV1Decoder(TsDataType dataType)
    {
        _dataType = dataType;
        _bitWidth = dataType switch
        {
            TsDataType.Float => 32,
            TsDataType.Double => 64,
            _ => throw new NotSupportedException($"GorillaV1 decoding does not support {dataType}")
        };
        Reset();
    }
    
    public bool ReadBoolean(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("GorillaV1 decoding does not support boolean values");
    }
    
    public int ReadInt(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("GorillaV1 decoding does not support int values");
    }
    
    public long ReadLong(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("GorillaV1 decoding does not support long values");
    }
    
    public float ReadFloat(byte[] buffer, ref int offset)
    {
        if (_floatQueue.Count == 0)
            DecodeAllFloat(buffer, ref offset);
        return _floatQueue.Dequeue();
    }
    
    public double ReadDouble(byte[] buffer, ref int offset)
    {
        if (_doubleQueue.Count == 0)
            DecodeAllDouble(buffer, ref offset);
        return _doubleQueue.Dequeue();
    }
    
    public string ReadString(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("GorillaV1 decoding does not support string values");
    }
    
    public byte[] ReadBytes(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("GorillaV1 decoding does not support byte array values");
    }
    
    public bool HasNext(byte[] buffer, int offset)
    {
        if (_dataType == TsDataType.Float && _floatQueue.Count > 0) return true;
        if (_dataType == TsDataType.Double && _doubleQueue.Count > 0) return true;
        return offset < buffer.Length;
    }
    
    public void Reset()
    {
        _flag = false;
        _isEnd = false;
        _numberLeftInBuffer = 0;
        _bitBuffer = -1;
        _floatQueue.Clear();
        _doubleQueue.Clear();
    }
    
    /// <summary>
    /// Decode all float values matching Java SinglePrecisionDecoderV1.
    /// Java V1 format: first value as 4 bytes little-endian, then XOR-compressed bits.
    /// Uses LEADING_ZERO_BITS_LENGTH_32BIT=5, FLOAT_VALUE_LENGTH=6.
    /// Ending marker: Float.NaN.
    /// </summary>
    private void DecodeAllFloat(byte[] buffer, ref int offset)
    {
        _sourceBuffer = buffer;
        _sourceOffset = offset;
        
        // Read first value: 4 bytes little-endian
        int ch1 = _sourceBuffer[_sourceOffset++];
        int ch2 = _sourceBuffer[_sourceOffset++];
        int ch3 = _sourceBuffer[_sourceOffset++];
        int ch4 = _sourceBuffer[_sourceOffset++];
        int preValue = ch1 + (ch2 << 8) + (ch3 << 16) + (ch4 << 24);
        _leadingZeroNum = NumberOfLeadingZeros32(preValue);
        _tailingZeroNum = NumberOfTrailingZeros32(preValue);
        
        float firstFloat = BitConverter.Int32BitsToSingle(preValue);
        
        // Fill bit buffer and pre-fetch next value
        FillBuffer();
        
        // Pre-fetch: read next value's control bits
        bool isEnd = false;
        (preValue, isEnd) = GetNextValueFloat(preValue);
        
        // Return first value (it's not NaN since it was written by the encoder)
        if (!float.IsNaN(firstFloat))
            _floatQueue.Enqueue(firstFloat);
        
        while (!isEnd)
        {
            float val = BitConverter.Int32BitsToSingle(preValue);
            if (float.IsNaN(val))
                break;
            _floatQueue.Enqueue(val);
            (preValue, isEnd) = GetNextValueFloat(preValue);
        }
        
        offset = _sourceOffset;
    }
    
    private (int preValue, bool isEnd) GetNextValueFloat(int preValue)
    {
        try
        {
            bool nextFlag1 = ReadBit();
            if (!nextFlag1)
            {
                // case '0': same value
                return (preValue, false);
            }
            
            bool nextFlag2 = ReadBit();
            if (!nextFlag2)
            {
                // case '10': use existing leading/trailing zeros
                int tmp = 0;
                int significantBits = 32 - _leadingZeroNum - _tailingZeroNum;
                for (int i = 0; i < significantBits; i++)
                {
                    int bit = ReadBit() ? 1 : 0;
                    tmp |= bit << (32 - 1 - _leadingZeroNum - i);
                }
                tmp ^= preValue;
                preValue = tmp;
            }
            else
            {
                // case '11': new leading zeros
                int leadingZeroNumTmp = ReadIntFromBuffer(5); // LEADING_ZERO_BITS_LENGTH_32BIT
                int lenTmp = ReadIntFromBuffer(6); // FLOAT_VALUE_LENGTH
                int tmp = ReadIntFromBuffer(lenTmp);
                tmp <<= (32 - leadingZeroNumTmp - lenTmp);
                tmp ^= preValue;
                preValue = tmp;
            }
            
            _leadingZeroNum = NumberOfLeadingZeros32(preValue);
            _tailingZeroNum = NumberOfTrailingZeros32(preValue);
            
            if (float.IsNaN(BitConverter.Int32BitsToSingle(preValue)))
                return (preValue, true);
            
            return (preValue, false);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return (preValue, true);
        }
    }
    
    /// <summary>
    /// Decode all double values matching Java DoublePrecisionDecoderV1.
    /// Java V1 format: first value as 8 bytes little-endian, then XOR-compressed bits.
    /// Uses LEADING_ZERO_BITS_LENGTH_64BIT=6, DOUBLE_VALUE_LENGTH=7.
    /// Ending marker: Double.NaN.
    /// </summary>
    private void DecodeAllDouble(byte[] buffer, ref int offset)
    {
        _sourceBuffer = buffer;
        _sourceOffset = offset;
        
        // Read first value: 8 bytes little-endian
        long preValue = 0;
        for (int i = 0; i < 8; i++)
            preValue += ((long)_sourceBuffer[_sourceOffset++] << (i * 8));
        
        _leadingZeroNum = NumberOfLeadingZeros64(preValue);
        _tailingZeroNum = NumberOfTrailingZeros64(preValue);
        
        double firstDouble = BitConverter.Int64BitsToDouble(preValue);
        
        FillBuffer();
        
        bool isEnd = false;
        (preValue, isEnd) = GetNextValueDouble(preValue);
        
        if (!double.IsNaN(firstDouble))
            _doubleQueue.Enqueue(firstDouble);
        
        while (!isEnd)
        {
            double val = BitConverter.Int64BitsToDouble(preValue);
            if (double.IsNaN(val))
                break;
            _doubleQueue.Enqueue(val);
            (preValue, isEnd) = GetNextValueDouble(preValue);
        }
        
        offset = _sourceOffset;
    }
    
    private (long preValue, bool isEnd) GetNextValueDouble(long preValue)
    {
        try
        {
            bool nextFlag1 = ReadBit();
            if (!nextFlag1)
            {
                return (preValue, false);
            }
            
            bool nextFlag2 = ReadBit();
            if (!nextFlag2)
            {
                // case '10'
                long tmp = 0;
                int significantBits = 64 - _leadingZeroNum - _tailingZeroNum;
                for (int i = 0; i < significantBits; i++)
                {
                    long bit = ReadBit() ? 1L : 0L;
                    tmp |= (bit << (64 - 1 - _leadingZeroNum - i));
                }
                tmp ^= preValue;
                preValue = tmp;
            }
            else
            {
                // case '11'
                int leadingZeroNumTmp = ReadIntFromBuffer(6); // LEADING_ZERO_BITS_LENGTH_64BIT
                int lenTmp = ReadIntFromBuffer(7); // DOUBLE_VALUE_LENGTH
                long tmp = ReadLongFromBuffer(lenTmp);
                tmp <<= (64 - leadingZeroNumTmp - lenTmp);
                tmp ^= preValue;
                preValue = tmp;
            }
            
            _leadingZeroNum = NumberOfLeadingZeros64(preValue);
            _tailingZeroNum = NumberOfTrailingZeros64(preValue);
            
            if (double.IsNaN(BitConverter.Int64BitsToDouble(preValue)))
                return (preValue, true);
            
            return (preValue, false);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return (preValue, true);
        }
    }
    
    private bool ReadBit()
    {
        if (_numberLeftInBuffer == 0 && !_isEnd)
        {
            FillBuffer();
        }
        if (_bitBuffer == -1)
        {
            throw new InvalidOperationException("Reading from empty buffer");
        }
        _numberLeftInBuffer--;
        return ((_bitBuffer >> _numberLeftInBuffer) & 1) == 1;
    }
    
    private void FillBuffer()
    {
        if (_sourceOffset < _sourceBuffer.Length)
        {
            _bitBuffer = _sourceBuffer[_sourceOffset++];
            _numberLeftInBuffer = 8;
        }
        else
        {
            _bitBuffer = -1;
            _numberLeftInBuffer = -1;
            _isEnd = true;
        }
    }
    
    private int ReadIntFromBuffer(int len)
    {
        int num = 0;
        for (int i = 0; i < len; i++)
        {
            int bit = ReadBit() ? 1 : 0;
            num |= bit << (len - 1 - i);
        }
        return num;
    }
    
    private long ReadLongFromBuffer(int len)
    {
        long num = 0;
        for (int i = 0; i < len; i++)
        {
            long bit = ReadBit() ? 1 : 0;
            num |= bit << (len - 1 - i);
        }
        return num;
    }
    
    private static int NumberOfLeadingZeros32(int value)
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
    
    private static int NumberOfTrailingZeros32(int value)
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
    
    private static int NumberOfLeadingZeros64(long value)
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
    
    private static int NumberOfTrailingZeros64(long value)
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
