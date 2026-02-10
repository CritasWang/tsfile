using System;
using System.Collections.Generic;
using System.IO;
using Apache.TsFile.Enums;

namespace Apache.TsFile.Encoding.Encoder
{
    /// <summary>
    /// TS_2DIFF (DeltaBinary) encoder matching Java's DeltaBinaryEncoder.
    /// Block-based bit-packing format:
    ///   [packNum:Int32BE][packWidth:Int32BE][minDeltaBase][firstValue][deltaBuf]
    /// </summary>
    public class Ts2DiffEncoder : IEncoder
    {
        private const int BlockDefaultSize = 128;
        private readonly TsDataType _dataType;
        private readonly int _blockSize;
        private readonly long[] _deltaBlockBuffer;
        private long _firstValue;
        private long _previousValue;
        private long _minDeltaBase;
        private int _writeIndex = -1;

        public Ts2DiffEncoder(TsDataType dataType) : this(dataType, BlockDefaultSize) { }

        public Ts2DiffEncoder(TsDataType dataType, int blockSize)
        {
            if (dataType != TsDataType.Int32 && dataType != TsDataType.Int64 &&
                dataType != TsDataType.Float && dataType != TsDataType.Double)
            {
                throw new ArgumentException(
                    $"TS_2DIFF encoding only supports Int32/Int64/Float/Double, got {dataType}");
            }
            _dataType = dataType;
            _blockSize = blockSize;
            _deltaBlockBuffer = new long[_blockSize];
            ResetBlock();
        }

        public void Encode(bool value, MemoryStream stream) =>
            throw new NotSupportedException("TS_2DIFF does not support boolean");

        public void Encode(int value, MemoryStream stream) => EncodeValue(value, stream);

        public void Encode(long value, MemoryStream stream) => EncodeValue(value, stream);

        public void Encode(float value, MemoryStream stream) =>
            EncodeValue(BitConverter.SingleToInt32Bits(value), stream);

        public void Encode(double value, MemoryStream stream) =>
            EncodeValue(BitConverter.DoubleToInt64Bits(value), stream);

        public void Encode(byte[] value, MemoryStream stream) =>
            throw new NotSupportedException("TS_2DIFF does not support byte arrays");

        public void Encode(string value, MemoryStream stream) =>
            throw new NotSupportedException("TS_2DIFF does not support strings");

        public void Flush(MemoryStream stream)
        {
            FlushBlockBuffer(stream);
        }

        public int GetOneItemMaxSize()
        {
            return (_dataType == TsDataType.Int32 || _dataType == TsDataType.Float) ? 4 : 8;
        }

        public long GetMaxByteSize()
        {
            int headerSize = (_dataType == TsDataType.Int32 || _dataType == TsDataType.Float)
                ? 4 + 4 + 4 + 4  // packNum + packWidth + minDeltaBase(int) + firstValue(int)
                : 4 + 4 + 8 + 8; // packNum + packWidth + minDeltaBase(long) + firstValue(long)
            return headerSize + (_writeIndex < 0 ? 0 : _writeIndex) * GetOneItemMaxSize();
        }

        private void EncodeValue(long value, MemoryStream stream)
        {
            if (_writeIndex == -1)
            {
                _writeIndex++;
                _firstValue = value;
                _previousValue = value;
                return;
            }
            long delta = value - _previousValue;
            if (delta < _minDeltaBase)
                _minDeltaBase = delta;
            _deltaBlockBuffer[_writeIndex++] = delta;
            _previousValue = value;
            if (_writeIndex == _blockSize)
                Flush(stream);
        }

        private void FlushBlockBuffer(MemoryStream stream)
        {
            if (_writeIndex == -1)
                return;

            int packNum = _writeIndex;

            // Subtract minDeltaBase from all deltas to make them non-negative
            for (int i = 0; i < packNum; i++)
                _deltaBlockBuffer[i] -= _minDeltaBase;

            // Calculate max bit width needed
            int packWidth = 0;
            for (int i = 0; i < packNum; i++)
            {
                int w = GetValueWidth(_deltaBlockBuffer[i]);
                if (w > packWidth) packWidth = w;
            }

            // Write header: [packNum:Int32BE][packWidth:Int32BE]
            WriteInt32BigEndian(stream, packNum);
            WriteInt32BigEndian(stream, packWidth);

            // Write type-specific header: [minDeltaBase][firstValue]
            if (_dataType == TsDataType.Int32 || _dataType == TsDataType.Float)
            {
                WriteInt32BigEndian(stream, (int)_minDeltaBase);
                WriteInt32BigEndian(stream, (int)_firstValue);
            }
            else
            {
                WriteInt64BigEndian(stream, _minDeltaBase);
                WriteInt64BigEndian(stream, _firstValue);
            }

            // Bit-pack deltas into encodingBlockBuffer
            int encodingLength = (int)Math.Ceiling(packNum * packWidth / 8.0);
            byte[] encodingBlockBuffer = new byte[encodingLength];
            for (int i = 0; i < packNum; i++)
                LongToBytes(_deltaBlockBuffer[i], encodingBlockBuffer, packWidth * i, packWidth);

            stream.Write(encodingBlockBuffer, 0, encodingLength);

            ResetBlock();
            _writeIndex = -1;
        }

        private void ResetBlock()
        {
            _firstValue = 0;
            _previousValue = 0;
            _minDeltaBase = (_dataType == TsDataType.Int32 || _dataType == TsDataType.Float)
                ? int.MaxValue : long.MaxValue;
        }

        private static int GetValueWidth(long v)
        {
            if (v == 0) return 0;
            return 64 - LeadingZeros(v);
        }

        private static int LeadingZeros(long v)
        {
            if (v == 0) return 64;
            int n = 0;
            ulong uv = (ulong)v;
            if (uv <= 0x00000000FFFFFFFFUL) { n += 32; uv <<= 32; }
            if (uv <= 0x0000FFFFFFFFFFFFUL) { n += 16; uv <<= 16; }
            if (uv <= 0x00FFFFFFFFFFFFFFUL) { n += 8; uv <<= 8; }
            if (uv <= 0x0FFFFFFFFFFFFFFFUL) { n += 4; uv <<= 4; }
            if (uv <= 0x3FFFFFFFFFFFFFFFUL) { n += 2; uv <<= 2; }
            if (uv <= 0x7FFFFFFFFFFFFFFFUL) { n += 1; }
            return n;
        }

        /// <summary>
        /// Write value into byte array at bit position pos with given bit width.
        /// Matches Java BytesUtils.longToBytes(long, byte[], int pos, int width).
        /// </summary>
        private static void LongToBytes(long srcNum, byte[] result, int pos, int width)
        {
            int cnt = pos & 0x07;
            int index = pos >> 3;
            while (width > 0)
            {
                int m = (width + cnt >= 8) ? (8 - cnt) : width;
                width -= m;
                int mask = 1 << (8 - cnt);
                cnt += m;
                byte y = (byte)(srcNum >> width);
                y = (byte)(y << (8 - cnt));
                mask = ~(mask - (1 << (8 - cnt)));
                result[index] = (byte)(result[index] & mask | y);
                srcNum = srcNum & ~(-1L << width);
                if (cnt == 8)
                {
                    index++;
                    cnt = 0;
                }
            }
        }

        private static void WriteInt32BigEndian(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static void WriteInt64BigEndian(Stream stream, long value)
        {
            stream.WriteByte((byte)(value >> 56));
            stream.WriteByte((byte)(value >> 48));
            stream.WriteByte((byte)(value >> 40));
            stream.WriteByte((byte)(value >> 32));
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }
    }
}
