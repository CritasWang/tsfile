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

namespace Apache.TsFile.Encoding.Decoder;

/// <summary>
/// CAMEL decoder - specialized double-precision compression.
/// Splits doubles into integer + decimal parts with Gorilla fallback.
/// Compatible with Java CamelDecoder.
/// </summary>
public class CamelDecoder : IDecoder
{
    private const int BitsForSign = 1;
    private const int BitsForType = 1;
    private const int BitsForFirstValue = 64;
    private const int BitsForLeadingZeros = 6;
    private const int BitsForSignificantBits = 6;
    private const int BitsForDecimalCount = 4;
    private const int DoubleTotalBits = 64;
    private const int DoubleMantissaBits = 52;
    private const int DecimalMaxCount = 15;

    private static readonly long[] Powers = new long[DecimalMaxCount];
    private static readonly long[] Threshold = new long[DecimalMaxCount];

    static CamelDecoder()
    {
        for (int l = 1; l <= DecimalMaxCount; l++)
        {
            int idx = l - 1;
            Powers[idx] = (long)Math.Pow(10, l);
            long divisor = 1L << l;
            Threshold[idx] = Powers[idx] / divisor;
        }
    }

    // Camel state
    private long _previousValue;
    private bool _isFirst = true;
    private long _storedVal;
    private double _scale;

    // Gorilla state
    private int _gorillaLeadingZeros = int.MaxValue;
    private int _gorillaTrailingZeros;

    // Block cache
    private double[]? _valueCache;
    private int _cacheIndex;
    private int _cacheSize;

    public bool HasNext(byte[] data, int offset)
    {
        if (_cacheIndex < _cacheSize) return true;
        return offset < data.Length;
    }

    public void Reset()
    {
        _isFirst = true;
        _previousValue = 0;
        _storedVal = 0;
        _gorillaLeadingZeros = int.MaxValue;
        _gorillaTrailingZeros = 0;
        _valueCache = null;
        _cacheIndex = 0;
        _cacheSize = 0;
    }

    public bool ReadBoolean(byte[] data, ref int offset) => throw new NotSupportedException();
    public int ReadInt(byte[] data, ref int offset) => throw new NotSupportedException();
    public long ReadLong(byte[] data, ref int offset) => throw new NotSupportedException();
    public float ReadFloat(byte[] data, ref int offset) => throw new NotSupportedException();
    public string ReadString(byte[] data, ref int offset) => throw new NotSupportedException();
    public byte[] ReadBytes(byte[] data, ref int offset) => throw new NotSupportedException();

    public double ReadDouble(byte[] data, ref int offset)
    {
        if (_cacheIndex < _cacheSize)
        {
            return _valueCache![_cacheIndex++];
        }

        // Read next block: [writtenBits: VarInt][bit data]
        int blockBits = ReadVarInt(data, ref offset);

        // Calculate how many bytes this block occupies
        int blockBytes = (blockBits + 7) / 8;
        var blockData = new byte[blockBytes];
        int toCopy = Math.Min(blockBytes, data.Length - offset);
        Array.Copy(data, offset, blockData, 0, toCopy);
        offset += blockBytes;

        // Reset state for new block
        _isFirst = true;
        _storedVal = 0;
        _previousValue = 0;
        _gorillaLeadingZeros = int.MaxValue;
        _gorillaTrailingZeros = 0;

        // Decode all values from this block using bit-tracking reader
        var reader = new CamelBitReader(blockData, blockBits);
        var values = new List<double>();

        while (reader.AvailableBits > 0)
        {
            double val = DecodeNext(reader);
            values.Add(val);
        }

        _valueCache = values.ToArray();
        _cacheSize = values.Count;
        _cacheIndex = 1;
        return _valueCache[0];
    }

    private double DecodeNext(CamelBitReader reader)
    {
        double result;
        if (_isFirst)
        {
            _isFirst = false;
            long firstBits = reader.ReadLong(BitsForFirstValue);
            result = BitConverter.Int64BitsToDouble(firstBits);
            _storedVal = (long)result;
        }
        else
        {
            result = DecodeNextValue(reader);
        }
        _previousValue = BitConverter.DoubleToInt64Bits(result);
        return result;
    }

    private double DecodeNextValue(CamelBitReader reader)
    {
        int signBit = reader.ReadInt(BitsForSign);
        double sign = signBit == 1 ? -1.0 : 1.0;

        int typeBit = reader.ReadInt(BitsForType);
        bool useCamel = typeBit == 1;

        if (useCamel)
        {
            long diff = ReadVarLongFromBits(reader);
            _storedVal += diff;
            long currentInt = _storedVal;

            double decPart = ReadDecimal(reader);
            double value = currentInt >= 0
                ? (currentInt * _scale + decPart) / _scale
                : -(currentInt * _scale + decPart) / _scale;
            return sign * value;
        }
        else
        {
            return sign * GorillaDecodeDouble(reader);
        }
    }

    private double GorillaDecodeDouble(CamelBitReader reader)
    {
        if (_isFirst)
        {
            _previousValue = reader.ReadLong(BitsForFirstValue);
            _isFirst = false;
            return BitConverter.Int64BitsToDouble(_previousValue);
        }

        bool controlBit = reader.ReadBool();
        if (!controlBit)
        {
            return BitConverter.Int64BitsToDouble(_previousValue);
        }

        bool reuseBlock = !reader.ReadBool();
        long xor;
        if (reuseBlock)
        {
            int sigBits = DoubleTotalBits - _gorillaLeadingZeros - _gorillaTrailingZeros;
            if (sigBits == 0)
            {
                return BitConverter.Int64BitsToDouble(_previousValue);
            }
            xor = reader.ReadLong(sigBits) << _gorillaTrailingZeros;
        }
        else
        {
            _gorillaLeadingZeros = reader.ReadInt(BitsForLeadingZeros);
            int sigBits = reader.ReadInt(BitsForSignificantBits) + 1;
            _gorillaTrailingZeros = DoubleTotalBits - _gorillaLeadingZeros - sigBits;
            xor = reader.ReadLong(sigBits) << _gorillaTrailingZeros;
        }

        _previousValue ^= xor;
        return BitConverter.Int64BitsToDouble(_previousValue);
    }

    private double ReadDecimal(CamelBitReader reader)
    {
        int count = reader.ReadInt(BitsForDecimalCount) + 1;
        bool hasXor = reader.ReadBool();
        long xor = 0;
        if (hasXor)
        {
            long bits = reader.ReadLong(count);
            xor = bits << (DoubleMantissaBits - count);
        }
        long mVal = ReadVarLongFromBits(reader);
        double frac;
        if (hasXor)
        {
            double baseVal = (double)mVal / Powers[count - 1] + 1;
            long merged = xor ^ BitConverter.DoubleToInt64Bits(baseVal);
            frac = BitConverter.Int64BitsToDouble(merged) - 1;
        }
        else
        {
            frac = (double)mVal / Powers[count - 1];
        }
        _scale = Math.Pow(10, count);
        return Math.Round(frac * _scale);
    }

    private static long ReadVarLongFromBits(CamelBitReader reader)
    {
        long result = 0;
        int shift = 0;

        while (true)
        {
            long chunk = reader.ReadLong(7);
            bool hasNext = reader.ReadBool();

            result |= chunk << shift;
            shift += 7;

            if (!hasNext) break;
            if (shift >= 64) throw new InvalidDataException("VarLong overflow");
        }

        // ZigZag decode
        return ((long)((ulong)result >> 1)) ^ -(result & 1);
    }

    private static int ReadVarInt(byte[] data, ref int offset)
    {
        // Java ReadWriteForEncodingUtils.readVarInt: ZigZag VarInt
        int result = 0;
        int shift = 0;
        while (true)
        {
            if (offset >= data.Length) throw new InvalidDataException("VarInt overflow");
            byte b = data[offset++];
            result |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        // ZigZag decode
        return (int)(((uint)result >> 1) ^ -(result & 1));
    }

    /// <summary>
    /// Bit-level reader with total bit count tracking for CAMEL block decoding.
    /// </summary>
    private class CamelBitReader
    {
        private readonly byte[] _buffer;
        private readonly int _totalBits;
        private int _bitsRead;

        public CamelBitReader(byte[] buffer, int totalBits)
        {
            _buffer = buffer;
            _totalBits = totalBits;
            _bitsRead = 0;
        }

        public int AvailableBits => _totalBits - _bitsRead;

        public bool ReadBool()
        {
            return ReadInt(1) != 0;
        }

        public int ReadInt(int numBits)
        {
            return (int)ReadLong(numBits);
        }

        public long ReadLong(int numBits)
        {
            long result = 0;
            for (int i = 0; i < numBits; i++)
            {
                int byteIndex = _bitsRead / 8;
                int bitIndex = _bitsRead % 8;
                if (byteIndex >= _buffer.Length)
                    return result;
                int bit = (_buffer[byteIndex] >> (7 - bitIndex)) & 1;
                result = (result << 1) | (uint)bit;
                _bitsRead++;
            }
            return result;
        }
    }
}
