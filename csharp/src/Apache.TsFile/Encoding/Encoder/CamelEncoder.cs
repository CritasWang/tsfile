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

namespace Apache.TsFile.Encoding.Encoder;

/// <summary>
/// CAMEL encoder - specialized double-precision compression.
/// Splits doubles into integer + decimal parts with Gorilla fallback.
/// Compatible with Java CamelEncoder.
/// </summary>
public class CamelEncoder : IEncoder
{
    private const int BitsForSign = 1;
    private const int BitsForType = 1;
    private const int BitsForFirstValue = 64;
    private const int BitsForLeadingZeros = 6;
    private const int BitsForSignificantBits = 6;
    private const int BitsForDecimalCount = 4;
    private const int DoubleTotalBits = 64;
    private const int DoubleMantissaBits = 52;
    private const int DecimalMaxCount = 10;

    private static readonly long[] Powers = new long[DecimalMaxCount];
    private static readonly long[] ThresholdValues = new long[DecimalMaxCount];

    static CamelEncoder()
    {
        for (int l = 1; l <= DecimalMaxCount; l++)
        {
            int idx = l - 1;
            Powers[idx] = (long)Math.Pow(10, l);
            long divisor = 1L << l;
            ThresholdValues[idx] = Powers[idx] / divisor;
        }
    }

    // Camel state
    private long _storedVal;
    private bool _isFirst = true;
    private long _previousValue;
    private bool _hasPending;

    // Gorilla state
    private int _gorillaLeadingZeros = int.MaxValue;
    private int _gorillaTrailingZeros;

    // Bit output
    private readonly CamelBitWriter _bitWriter = new();

    public void Encode(bool value, MemoryStream stream) => throw new NotSupportedException("CAMEL only supports Double");
    public void Encode(int value, MemoryStream stream) => throw new NotSupportedException("CAMEL only supports Double");
    public void Encode(long value, MemoryStream stream) => throw new NotSupportedException("CAMEL only supports Double");
    public void Encode(float value, MemoryStream stream) => throw new NotSupportedException("CAMEL only supports Double");
    public void Encode(string value, MemoryStream stream) => throw new NotSupportedException("CAMEL only supports Double");
    public void Encode(byte[] value, MemoryStream stream) => throw new NotSupportedException("CAMEL only supports Double");

    public void Encode(double value, MemoryStream stream)
    {
        AddValue(value);
        _hasPending = true;
    }

    public void Flush(MemoryStream stream)
    {
        if (!_hasPending) return;

        _bitWriter.Close();
        int writtenBits = _bitWriter.BitsWritten;

        // Write header: [writtenBits: VarInt]
        WriteVarInt(writtenBits, stream);

        // Write bit data
        var bytes = _bitWriter.ToArray();
        stream.Write(bytes, 0, bytes.Length);

        // Reset
        _bitWriter.Reset();
        ResetState();
        _hasPending = false;
    }

    public int GetOneItemMaxSize() => 8;
    public long GetMaxByteSize() => 1 + _bitWriter.ByteCount + 16;

    private void AddValue(double value)
    {
        if (_isFirst)
        {
            WriteFirst(BitConverter.DoubleToInt64Bits(value));
        }
        else
        {
            CompressValue(value);
        }
        _previousValue = BitConverter.DoubleToInt64Bits(value);
    }

    private void WriteFirst(long value)
    {
        _isFirst = false;
        _storedVal = (long)BitConverter.Int64BitsToDouble(value);
        _bitWriter.WriteLong(value, BitsForFirstValue);
    }

    private void CompressValue(double value)
    {
        int signBit = (int)(((ulong)BitConverter.DoubleToInt64Bits(value) >> (DoubleTotalBits - 1)) & 1);
        _bitWriter.WriteInt(signBit, BitsForSign);

        value = Math.Abs(value);
        if (value > long.MaxValue || value == 0 ||
            Math.Abs(Math.Floor(Math.Log10(value))) > DecimalMaxCount)
        {
            _bitWriter.WriteInt(0, BitsForType); // GORILLA
            GorillaEncode(value);
            return;
        }

        long integerPart = (long)value;
        int numDigits = 1;
        long absInt = Math.Abs(integerPart);
        while (absInt >= 10)
        {
            absInt /= 10;
            numDigits++;
        }

        double factor = 1;
        int decimalCount = 0;
        while (Math.Abs(value * factor - Math.Round(value * factor)) > 0)
        {
            factor *= 10.0;
            decimalCount++;
            if (numDigits + decimalCount > DecimalMaxCount) break;
        }

        decimalCount = Math.Max(1, decimalCount);

        if (decimalCount + numDigits <= DecimalMaxCount)
        {
            long pow = Powers[decimalCount - 1];
            long decimalValue = (long)Math.Round(value * pow) % pow;

            _bitWriter.WriteInt(1, BitsForType); // CAMEL
            CompressIntegerValue(integerPart);
            CompressDecimalValue(decimalValue, decimalCount);
        }
        else
        {
            _bitWriter.WriteInt(0, BitsForType); // GORILLA
            GorillaEncode(value);
        }
    }

    private void CompressIntegerValue(long value)
    {
        long diff = value - _storedVal;
        _storedVal = value;
        WriteVarLongToBits(diff);
    }

    private void CompressDecimalValue(long decimalValue, int decimalCount)
    {
        _bitWriter.WriteInt(decimalCount - 1, BitsForDecimalCount);
        long thresh = ThresholdValues[decimalCount - 1];
        int m = (int)decimalValue;

        if (decimalValue >= thresh)
        {
            _bitWriter.WriteBit(true);
            m = (int)(decimalValue % thresh);

            long xor = BitConverter.DoubleToInt64Bits((double)decimalValue / Powers[decimalCount - 1] + 1)
                     ^ BitConverter.DoubleToInt64Bits((double)m / Powers[decimalCount - 1] + 1);

            _bitWriter.WriteLong((long)((ulong)xor >> (DoubleMantissaBits - decimalCount)), decimalCount);
        }
        else
        {
            _bitWriter.WriteBit(false);
        }

        WriteVarLongToBits(m);
    }

    private void GorillaEncode(double value)
    {
        long curr = BitConverter.DoubleToInt64Bits(value);
        if (_isFirst)
        {
            _bitWriter.WriteLong(curr, BitsForFirstValue);
            _previousValue = curr;
            _isFirst = false;
            return;
        }

        long xor = curr ^ _previousValue;
        if (xor == 0)
        {
            _bitWriter.WriteBit(false);
        }
        else
        {
            _bitWriter.WriteBit(true);
            int leading = CountLeadingZeros(xor);
            int trailing = CountTrailingZeros(xor);

            if (leading >= _gorillaLeadingZeros && trailing >= _gorillaTrailingZeros)
            {
                _bitWriter.WriteBit(false);
                int significantBits = DoubleTotalBits - _gorillaLeadingZeros - _gorillaTrailingZeros;
                _bitWriter.WriteLong((long)((ulong)xor >> _gorillaTrailingZeros), significantBits);
            }
            else
            {
                _bitWriter.WriteBit(true);
                int significantBits = DoubleTotalBits - leading - trailing;
                _bitWriter.WriteInt(leading, BitsForLeadingZeros);
                _bitWriter.WriteInt(significantBits - 1, BitsForSignificantBits);
                _bitWriter.WriteLong((long)((ulong)xor >> trailing), significantBits);
                _gorillaLeadingZeros = leading;
                _gorillaTrailingZeros = trailing;
            }
        }
        _previousValue = curr;
    }

    private void WriteVarLongToBits(long value)
    {
        // ZigZag encode
        ulong uValue = (ulong)((value << 1) ^ (value >> 63));

        while ((uValue & ~0x7FUL) != 0)
        {
            int chunk = (int)(uValue & 0x7F);
            _bitWriter.WriteInt(chunk, 7);
            _bitWriter.WriteBit(true); // has more
            uValue >>= 7;
        }

        _bitWriter.WriteInt((int)(uValue & 0x7F), 7);
        _bitWriter.WriteBit(false); // end
    }

    private void ResetState()
    {
        _isFirst = true;
        _storedVal = 0;
        _previousValue = 0;
        _hasPending = false;
        _gorillaLeadingZeros = int.MaxValue;
        _gorillaTrailingZeros = 0;
    }

    private static int CountLeadingZeros(long value)
    {
        if (value == 0) return 64;
        int count = 0;
        ulong uv = (ulong)value;
        while ((uv & (1UL << 63)) == 0) { count++; uv <<= 1; }
        return count;
    }

    private static int CountTrailingZeros(long value)
    {
        if (value == 0) return 64;
        int count = 0;
        ulong uv = (ulong)value;
        while ((uv & 1) == 0) { count++; uv >>= 1; }
        return count;
    }

    private static void WriteVarInt(int value, MemoryStream stream)
    {
        // ZigZag encode
        uint uValue = (uint)((value << 1) ^ (value >> 31));
        while (uValue > 0x7F)
        {
            stream.WriteByte((byte)(uValue | 0x80));
            uValue >>= 7;
        }
        stream.WriteByte((byte)uValue);
    }

    /// <summary>
    /// Bit-level writer for CAMEL encoding with bit count tracking.
    /// </summary>
    private class CamelBitWriter
    {
        private readonly List<byte> _buffer = new();
        private byte _currentByte;
        private int _bitOffset;
        private int _bitsWritten;

        public int BitsWritten => _bitsWritten;
        public int ByteCount => _buffer.Count + (_bitOffset > 0 ? 1 : 0);

        public void WriteBit(bool bit)
        {
            if (bit)
                _currentByte |= (byte)(1 << (7 - _bitOffset));
            _bitOffset++;
            _bitsWritten++;
            if (_bitOffset == 8)
            {
                _buffer.Add(_currentByte);
                _currentByte = 0;
                _bitOffset = 0;
            }
        }

        public void WriteInt(int value, int numBits)
        {
            WriteLong(value, numBits);
        }

        public void WriteLong(long value, int numBits)
        {
            for (int i = numBits - 1; i >= 0; i--)
            {
                WriteBit(((value >> i) & 1) != 0);
            }
        }

        public void Close()
        {
            // Flush remaining bits
            if (_bitOffset > 0)
            {
                _buffer.Add(_currentByte);
                _currentByte = 0;
                _bitOffset = 0;
            }
        }

        public byte[] ToArray() => _buffer.ToArray();

        public void Reset()
        {
            _buffer.Clear();
            _currentByte = 0;
            _bitOffset = 0;
            _bitsWritten = 0;
        }
    }
}
