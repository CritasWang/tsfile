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
/// RLE (Run-Length Encoding) encoder using hybrid RLE + bit-packing approach.
/// Encodes runs of repeated values efficiently while maintaining good compression for varied data.
/// </summary>
public class RleEncoder : IEncoder
{
    private const int RleMinRepeatedNum = 8;      // Minimum repeats to trigger RLE
    private const int RleMaxRepeatedNum = 0x7FFF;  // Max repeats per RLE run (32,767)
    private const int RleMaxBitPackedNum = 63;     // Max groups in one bit-packed run
    private const int BitPackedGroupSize = 8;      // Values per bit-packed group
    
    private readonly List<int> _intValues = new();
    private readonly List<long> _longValues = new();
    private readonly List<bool> _boolValues = new();
    private readonly TsDataType _dataType;
    
    public RleEncoder(TsDataType dataType)
    {
        _dataType = dataType;
    }
    
    public void Encode(bool value, MemoryStream stream)
    {
        _boolValues.Add(value);
    }
    
    public void Encode(int value, MemoryStream stream)
    {
        _intValues.Add(value);
    }
    
    public void Encode(long value, MemoryStream stream)
    {
        _longValues.Add(value);
    }
    
    public void Encode(float value, MemoryStream stream)
    {
        // RLE not typically used for floats, fall back to int representation
        _intValues.Add(BitConverter.SingleToInt32Bits(value));
    }
    
    public void Encode(double value, MemoryStream stream)
    {
        // RLE not typically used for doubles, fall back to long representation
        _longValues.Add(BitConverter.DoubleToInt64Bits(value));
    }
    
    public void Encode(string value, MemoryStream stream)
    {
        throw new NotSupportedException("RLE encoding does not support string values");
    }
    
    public void Encode(byte[] value, MemoryStream stream)
    {
        throw new NotSupportedException("RLE encoding does not support byte array values");
    }
    
    public void Flush(MemoryStream stream)
    {
        if (_boolValues.Count > 0)
        {
            EncodeIntegers(stream, _boolValues.Select(b => b ? 1 : 0).ToList());
            _boolValues.Clear();
        }
        else if (_intValues.Count > 0)
        {
            EncodeIntegers(stream, _intValues);
            _intValues.Clear();
        }
        else if (_longValues.Count > 0)
        {
            EncodeLongs(stream, _longValues);
            _longValues.Clear();
        }
    }
    
    public int GetOneItemMaxSize()
    {
        return _dataType switch
        {
            TsDataType.Boolean or TsDataType.Int32 or TsDataType.Float => 45,
            TsDataType.Int64 or TsDataType.Double or TsDataType.Timestamp => 77,
            _ => 8
        };
    }
    
    public long GetMaxByteSize()
    {
        return (_boolValues.Count + _intValues.Count) * 45L + _longValues.Count * 77L;
    }
    
    private void EncodeIntegers(MemoryStream stream, List<int> values)
    {
        if (values.Count == 0) return;
        
        // Calculate bit width required
        int bitWidth = CalculateBitWidth(values);
        
        using var buffer = new MemoryStream();
        buffer.WriteByte((byte)bitWidth);
        
        EncodeIntRuns(buffer, values, bitWidth);
        
        // Write length (unsignedVarInt) and data - matches Java format
        WriteUnsignedVarInt(stream, (int)buffer.Length);
        buffer.Position = 0;
        buffer.CopyTo(stream);
    }
    
    private void EncodeLongs(MemoryStream stream, List<long> values)
    {
        if (values.Count == 0) return;
        
        int bitWidth = CalculateBitWidth(values);
        
        using var buffer = new MemoryStream();
        buffer.WriteByte((byte)bitWidth);
        
        EncodeLongRuns(buffer, values, bitWidth);
        
        WriteUnsignedVarInt(stream, (int)buffer.Length);
        buffer.Position = 0;
        buffer.CopyTo(stream);
    }
    
    private void EncodeIntRuns(MemoryStream stream, List<int> values, int bitWidth)
    {
        int i = 0;
        while (i < values.Count)
        {
            int currentValue = values[i];
            int repeatCount = 1;
            
            // Count consecutive repeated values
            while (i + repeatCount < values.Count && values[i + repeatCount] == currentValue)
            {
                repeatCount++;
            }
            
            if (repeatCount >= RleMinRepeatedNum)
            {
                // Emit RLE run(s)
                int totalRepeat = repeatCount;
                while (totalRepeat > 0)
                {
                    int runLength = Math.Min(totalRepeat, RleMaxRepeatedNum);
                    WriteVarInt(stream, runLength << 1); // LSB = 0 for RLE
                    WritePaddedInt(stream, currentValue, bitWidth);
                    totalRepeat -= runLength;
                }
                i += repeatCount;
            }
            else
            {
                // Collect values for bit-packing
                var bitPackedValues = new List<int>();
                while (i < values.Count && bitPackedValues.Count < RleMaxBitPackedNum * BitPackedGroupSize)
                {
                    currentValue = values[i];
                    repeatCount = 1;
                    while (i + repeatCount < values.Count && values[i + repeatCount] == currentValue)
                    {
                        repeatCount++;
                    }
                    
                    if (repeatCount >= RleMinRepeatedNum)
                        break; // Stop before RLE run
                    
                    bitPackedValues.Add(currentValue);
                    i++;
                }
                
                if (bitPackedValues.Count > 0)
                {
                    EmitBitPackedRun(stream, bitPackedValues, bitWidth);
                }
            }
        }
    }
    
    private void EncodeLongRuns(MemoryStream stream, List<long> values, int bitWidth)
    {
        int i = 0;
        while (i < values.Count)
        {
            long currentValue = values[i];
            int repeatCount = 1;
            
            while (i + repeatCount < values.Count && values[i + repeatCount] == currentValue)
            {
                repeatCount++;
            }
            
            if (repeatCount >= RleMinRepeatedNum)
            {
                int totalRepeat = repeatCount;
                while (totalRepeat > 0)
                {
                    int runLength = Math.Min(totalRepeat, RleMaxRepeatedNum);
                    WriteVarInt(stream, runLength << 1);
                    WritePaddedLong(stream, currentValue, bitWidth);
                    totalRepeat -= runLength;
                }
                i += repeatCount;
            }
            else
            {
                var bitPackedValues = new List<long>();
                while (i < values.Count && bitPackedValues.Count < RleMaxBitPackedNum * BitPackedGroupSize)
                {
                    currentValue = values[i];
                    repeatCount = 1;
                    while (i + repeatCount < values.Count && values[i + repeatCount] == currentValue)
                    {
                        repeatCount++;
                    }
                    
                    if (repeatCount >= RleMinRepeatedNum)
                        break;
                    
                    bitPackedValues.Add(currentValue);
                    i++;
                }
                
                if (bitPackedValues.Count > 0)
                {
                    EmitBitPackedRunLong(stream, bitPackedValues, bitWidth);
                }
            }
        }
    }
    
    private void EmitBitPackedRun(MemoryStream stream, List<int> values, int bitWidth)
    {
        int groupCount = (values.Count + BitPackedGroupSize - 1) / BitPackedGroupSize;
        int lastNum = values.Count % BitPackedGroupSize;
        if (lastNum == 0) lastNum = BitPackedGroupSize;
        
        WriteVarInt(stream, (groupCount << 1) | 1); // LSB = 1 for bit-packed
        stream.WriteByte((byte)lastNum);
        
        // Pack values in groups of 8
        for (int g = 0; g < groupCount; g++)
        {
            int start = g * BitPackedGroupSize;
            int count = Math.Min(BitPackedGroupSize, values.Count - start);
            PackInts(stream, values.Skip(start).Take(count).ToList(), bitWidth);
        }
    }
    
    private void EmitBitPackedRunLong(MemoryStream stream, List<long> values, int bitWidth)
    {
        int groupCount = (values.Count + BitPackedGroupSize - 1) / BitPackedGroupSize;
        int lastNum = values.Count % BitPackedGroupSize;
        if (lastNum == 0) lastNum = BitPackedGroupSize;
        
        WriteVarInt(stream, (groupCount << 1) | 1);
        stream.WriteByte((byte)lastNum);
        
        for (int g = 0; g < groupCount; g++)
        {
            int start = g * BitPackedGroupSize;
            int count = Math.Min(BitPackedGroupSize, values.Count - start);
            PackLongs(stream, values.Skip(start).Take(count).ToList(), bitWidth);
        }
    }
    
    private void PackInts(MemoryStream stream, List<int> values, int bitWidth)
    {
        // Java IntPacker format: big-endian bit packing
        // Values are packed MSB-first into a 32-bit buffer, written as big-endian bytes
        // Each group of 8 values always occupies exactly bitWidth bytes
        if (bitWidth == 0) return;
        
        // Pad to 8 values (Java always packs 8)
        var padded = new int[8];
        for (int i = 0; i < Math.Min(values.Count, 8); i++)
            padded[i] = values[i];
        
        var packed = new byte[bitWidth];
        int bufIdx = 0;
        int valueIdx = 0;
        int leftBit = 0;
        
        while (valueIdx < 8)
        {
            int buffer = 0;
            int leftSize = 32;
            
            if (leftBit > 0)
            {
                buffer |= (padded[valueIdx] << (32 - leftBit));
                leftSize -= leftBit;
                leftBit = 0;
                valueIdx++;
            }
            
            while (leftSize >= bitWidth && valueIdx < 8)
            {
                buffer |= (padded[valueIdx] << (leftSize - bitWidth));
                leftSize -= bitWidth;
                valueIdx++;
            }
            
            if (leftSize > 0 && valueIdx < 8)
            {
                buffer |= (int)((uint)padded[valueIdx] >> (bitWidth - leftSize));
                leftBit = bitWidth - leftSize;
            }
            
            for (int j = 0; j < 4; j++)
            {
                packed[bufIdx] = (byte)((buffer >> ((3 - j) * 8)) & 0xFF);
                bufIdx++;
                if (bufIdx >= bitWidth) goto done;
            }
        }
        done:
        stream.Write(packed, 0, bitWidth);
    }
    
    private void PackLongs(MemoryStream stream, List<long> values, int bitWidth)
    {
        // Java LongPacker format: big-endian bit packing (same as IntPacker but 64-bit)
        if (bitWidth == 0) return;
        
        var padded = new long[8];
        for (int i = 0; i < Math.Min(values.Count, 8); i++)
            padded[i] = values[i];
        
        var packed = new byte[bitWidth];
        int bufIdx = 0;
        int valueIdx = 0;
        int leftBit = 0;
        
        while (valueIdx < 8)
        {
            long buffer = 0;
            int leftSize = 64;
            
            if (leftBit > 0)
            {
                buffer |= (padded[valueIdx] << (64 - leftBit));
                leftSize -= leftBit;
                leftBit = 0;
                valueIdx++;
            }
            
            while (leftSize >= bitWidth && valueIdx < 8)
            {
                buffer |= (padded[valueIdx] << (leftSize - bitWidth));
                leftSize -= bitWidth;
                valueIdx++;
            }
            
            if (leftSize > 0 && valueIdx < 8)
            {
                buffer |= (long)((ulong)padded[valueIdx] >> (bitWidth - leftSize));
                leftBit = bitWidth - leftSize;
            }
            
            for (int j = 0; j < 8; j++)
            {
                packed[bufIdx] = (byte)((buffer >> ((7 - j) * 8)) & 0xFF);
                bufIdx++;
                if (bufIdx >= bitWidth) goto done;
            }
        }
        done:
        stream.Write(packed, 0, bitWidth);
    }
    
    private int CalculateBitWidth(List<int> values)
    {
        if (values.Count == 0) return 1;
        
        // Use unsigned bit width (matches Java's getIntMaxBitWidth)
        int maxWidth = 1;
        foreach (var v in values)
        {
            int w = 32 - LeadingZeros((uint)v);
            if (w > maxWidth) maxWidth = w;
        }
        return maxWidth;
    }
    
    private int CalculateBitWidth(List<long> values)
    {
        if (values.Count == 0) return 1;
        
        // Use unsigned bit width (matches Java's getLongMaxBitWidth)
        int maxWidth = 1;
        foreach (var v in values)
        {
            int w = 64 - LeadingZeros((ulong)v);
            if (w > maxWidth) maxWidth = w;
        }
        return maxWidth;
    }
    
    private static int LeadingZeros(uint v)
    {
        if (v == 0) return 32;
        int n = 0;
        if (v <= 0x0000FFFF) { n += 16; v <<= 16; }
        if (v <= 0x00FFFFFF) { n += 8; v <<= 8; }
        if (v <= 0x0FFFFFFF) { n += 4; v <<= 4; }
        if (v <= 0x3FFFFFFF) { n += 2; v <<= 2; }
        if (v <= 0x7FFFFFFF) { n += 1; }
        return n;
    }
    
    private static int LeadingZeros(ulong v)
    {
        if (v == 0) return 64;
        int n = 0;
        if (v <= 0x00000000FFFFFFFFUL) { n += 32; v <<= 32; }
        if (v <= 0x0000FFFFFFFFFFFFUL) { n += 16; v <<= 16; }
        if (v <= 0x00FFFFFFFFFFFFFFUL) { n += 8; v <<= 8; }
        if (v <= 0x0FFFFFFFFFFFFFFFUL) { n += 4; v <<= 4; }
        if (v <= 0x3FFFFFFFFFFFFFFFUL) { n += 2; v <<= 2; }
        if (v <= 0x7FFFFFFFFFFFFFFFUL) { n += 1; }
        return n;
    }
    
    private static void WriteUnsignedVarInt(Stream stream, int value)
    {
        while ((value & ~0x7F) != 0)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value = (int)((uint)value >> 7);
        }
        stream.WriteByte((byte)value);
    }
    
    private static void WriteVarInt(Stream stream, int value)
    {
        while ((value & ~0x7F) != 0)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value = (int)((uint)value >> 7);
        }
        stream.WriteByte((byte)value);
    }
    
    private static void WritePaddedInt(Stream stream, int value, int bitWidth)
    {
        // Java writes RLE repeated value in little-endian byte order
        int byteWidth = (bitWidth + 7) / 8;
        for (int i = 0; i < byteWidth; i++)
        {
            stream.WriteByte((byte)(value >> (i * 8)));
        }
    }
    
    private static void WritePaddedLong(Stream stream, long value, int bitWidth)
    {
        // Java writeLongLittleEndianPaddedOnBitWidth uses BytesUtils.longToBytes which is BIG-ENDIAN
        int byteWidth = (bitWidth + 7) / 8;
        for (int i = byteWidth - 1; i >= 0; i--)
        {
            stream.WriteByte((byte)(value >> (i * 8)));
        }
    }
}
