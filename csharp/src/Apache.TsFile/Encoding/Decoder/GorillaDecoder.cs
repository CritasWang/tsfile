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
/// Gorilla decoder - decodes XOR-compressed time-series data.
/// </summary>
public class GorillaDecoder : IDecoder
{
    private BitReader? _bitReader;
    private long _previousValue;
    private int _previousLeadingZeros;
    private int _previousTrailingZeros;
    private bool _first = true;
    private readonly TsDataType _dataType;
    private readonly int _bitWidth;
    private readonly Queue<float> _floatQueue = new();
    private readonly Queue<double> _doubleQueue = new();
    private readonly Queue<int> _intQueue = new();
    private readonly Queue<long> _longQueue = new();
    
    public GorillaDecoder(TsDataType dataType)
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
    
    public bool ReadBoolean(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("Gorilla decoder does not support boolean values");
    }
    
    public int ReadInt(byte[] buffer, ref int offset)
    {
        EnsureData(buffer, ref offset);
        return _intQueue.Dequeue();
    }
    
    public long ReadLong(byte[] buffer, ref int offset)
    {
        EnsureData(buffer, ref offset);
        return _longQueue.Dequeue();
    }
    
    public float ReadFloat(byte[] buffer, ref int offset)
    {
        EnsureData(buffer, ref offset);
        return _floatQueue.Dequeue();
    }
    
    public double ReadDouble(byte[] buffer, ref int offset)
    {
        EnsureData(buffer, ref offset);
        return _doubleQueue.Dequeue();
    }
    
    public string ReadString(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("Gorilla decoder does not support string values");
    }
    
    public byte[] ReadBytes(byte[] buffer, ref int offset)
    {
        throw new NotSupportedException("Gorilla decoder does not support byte array values");
    }
    
    public bool HasNext(byte[] buffer, int offset)
    {
        return _floatQueue.Count > 0 || _doubleQueue.Count > 0 || 
               _intQueue.Count > 0 || _longQueue.Count > 0 || 
               offset < buffer.Length;
    }
    
    public void Reset()
    {
        _bitReader = null;
        _previousValue = 0;
        _previousLeadingZeros = 0;
        _previousTrailingZeros = 0;
        _first = true;
        _floatQueue.Clear();
        _doubleQueue.Clear();
        _intQueue.Clear();
        _longQueue.Clear();
    }
    
    private void EnsureData(byte[] buffer, ref int offset)
    {
        if (HasData()) return;
        
        if (_bitReader == null)
        {
            // Java GorillaEncoderV2 writes raw bit-packed data with no length prefix.
            // The entire remaining buffer is the encoded data.
            byte[] encodedData = new byte[buffer.Length - offset];
            Array.Copy(buffer, offset, encodedData, 0, encodedData.Length);
            offset = buffer.Length; // consume all remaining bytes
            
            _bitReader = new BitReader(encodedData);
            _first = true;
            _previousLeadingZeros = int.MaxValue;
            _previousTrailingZeros = 0;
        }
        
        // Decode all values at once (until ending marker)
        DecodeAll();
    }
    
    private void DecodeAll()
    {
        // Java Gorilla V2 uses a read-ahead pattern:
        // readInt() returns the cached storedValue, then pre-reads the next value via cacheNext().
        // The ending marker (Integer.MIN_VALUE / Long.MIN_VALUE) is detected in cacheNext()
        // and is never returned to the caller.
        
        int leadingZeroBits = _bitWidth == 32 ? 5 : 6;
        int meaningfulBitsField = _bitWidth == 32 ? 5 : 6;
        
        try
        {
            // Read first value (full bit width)
            long storedValue = _bitReader!.ReadBits(_bitWidth);
            
            // Pre-read next value (Java read-ahead: cacheNext)
            long nextValue = ReadNextValue(storedValue, leadingZeroBits, meaningfulBitsField);
            bool hasMore = !IsEndingMarker(nextValue);
            
            // Enqueue first value
            EnqueueValue(storedValue);
            storedValue = nextValue;
            
            while (hasMore)
            {
                nextValue = ReadNextValue(storedValue, leadingZeroBits, meaningfulBitsField);
                hasMore = !IsEndingMarker(nextValue);
                
                EnqueueValue(storedValue);
                storedValue = nextValue;
            }
        }
        catch
        {
            // End of stream — no more bits to read
        }
    }
    
    private long ReadNextValue(long currentValue, int leadingZeroBits, int meaningfulBitsField)
    {
        // Java readNextClearBit(2): reads up to 2 bits, stops at first 0-bit
        // Returns: 0 (read '0'), 2 (read '10'), 3 (read '11')
        byte controlBits = ReadNextClearBit(2);
        
        switch (controlBits)
        {
            case 3: // '11': new leading and trailing zeros
            {
                _previousLeadingZeros = (int)_bitReader!.ReadBits(leadingZeroBits);
                int significantBits = (int)_bitReader.ReadBits(meaningfulBitsField) + 1;
                _previousTrailingZeros = _bitWidth - significantBits - _previousLeadingZeros;
                // Fall through to case 2
                goto case 2;
            }
            case 2: // '10': use stored leading and trailing zeros
            {
                int meaningfulBits = _bitWidth - _previousLeadingZeros - _previousTrailingZeros;
                long xor = _bitReader!.ReadBits(meaningfulBits);
                xor <<= _previousTrailingZeros;
                _previousValue = currentValue ^ xor;
                return _previousValue;
            }
            default: // '0': value unchanged
            {
                _previousValue = currentValue;
                return currentValue;
            }
        }
    }
    
    /// <summary>
    /// Reads up to maxBits bits, stopping at the first 0-bit.
    /// Returns the accumulated value. Matches Java's readNextClearBit.
    /// </summary>
    private byte ReadNextClearBit(int maxBits)
    {
        byte value = 0;
        for (int i = 0; i < maxBits; i++)
        {
            value <<= 1;
            if (_bitReader!.ReadBit() == 1)
            {
                value |= 1;
            }
            else
            {
                break;
            }
        }
        return value;
    }
    
    private void EnqueueValue(long value)
    {
        switch (_dataType)
        {
            case TsDataType.Float:
                _floatQueue.Enqueue(BitConverter.Int32BitsToSingle((int)value));
                break;
            case TsDataType.Double:
                _doubleQueue.Enqueue(BitConverter.Int64BitsToDouble(value));
                break;
            case TsDataType.Int32:
                _intQueue.Enqueue((int)value);
                break;
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                _longQueue.Enqueue(value);
                break;
        }
    }
    
    private bool IsEndingMarker(long value)
    {
        if (_bitWidth == 32) return (int)value == int.MinValue;
        return value == long.MinValue;
    }
    
    private bool HasData()
    {
        return _floatQueue.Count > 0 || _doubleQueue.Count > 0 || 
               _intQueue.Count > 0 || _longQueue.Count > 0;
    }
}
