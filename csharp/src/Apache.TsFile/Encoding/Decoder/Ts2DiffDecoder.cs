using System;
using System.Collections.Generic;
using Apache.TsFile.Enums;

namespace Apache.TsFile.Encoding.Decoder
{
    /// <summary>
    /// TS_2DIFF (DeltaBinary) decoder matching Java's DeltaBinaryDecoder.
    /// Uses block-based bit-packing format:
    ///   [packNum:Int32BE][packWidth:Int32BE][minDeltaBase][firstValue][deltaBuf]
    /// For Int32/Float: minDeltaBase and firstValue are Int32 big-endian.
    /// For Int64/Double: minDeltaBase and firstValue are Int64 big-endian.
    /// </summary>
    public class Ts2DiffDecoder : IDecoder
    {
        private readonly TsDataType _dataType;
        private long[] _data = Array.Empty<long>();
        private int _readTotalCount;
        private int _nextReadIndex;

        public Ts2DiffDecoder(TsDataType dataType)
        {
            if (dataType != TsDataType.Int32 && dataType != TsDataType.Int64 &&
                dataType != TsDataType.Float && dataType != TsDataType.Double)
            {
                throw new ArgumentException(
                    $"TS_2DIFF decoding only supports Int32/Int64/Float/Double, got {dataType}");
            }
            _dataType = dataType;
        }

        public bool ReadBoolean(byte[] data, ref int offset) =>
            throw new NotSupportedException("TS_2DIFF does not support boolean");

        public int ReadInt(byte[] data, ref int offset)
        {
            if (_nextReadIndex == _readTotalCount)
                return (int)LoadBatch(data, ref offset);
            return (int)_data[_nextReadIndex++];
        }

        public long ReadLong(byte[] data, ref int offset)
        {
            if (_nextReadIndex == _readTotalCount)
                return LoadBatch(data, ref offset);
            return _data[_nextReadIndex++];
        }

        public float ReadFloat(byte[] data, ref int offset)
        {
            int bits = ReadInt(data, ref offset);
            return BitConverter.Int32BitsToSingle(bits);
        }

        public double ReadDouble(byte[] data, ref int offset)
        {
            long bits = ReadLong(data, ref offset);
            return BitConverter.Int64BitsToDouble(bits);
        }

        public byte[] ReadBytes(byte[] data, ref int offset) =>
            throw new NotSupportedException("TS_2DIFF does not support byte arrays");

        public string ReadString(byte[] data, ref int offset) =>
            throw new NotSupportedException("TS_2DIFF does not support strings");

        public bool HasNext(byte[] data, int offset)
        {
            return _nextReadIndex < _readTotalCount || offset < data.Length;
        }

        public void Reset()
        {
            _data = Array.Empty<long>();
            _readTotalCount = 0;
            _nextReadIndex = 0;
        }

        private long LoadBatch(byte[] data, ref int offset)
        {
            // Java format: [packNum:Int32BE][packWidth:Int32BE][header][deltaBuf]
            int packNum = ReadInt32BigEndian(data, ref offset);
            int packWidth = ReadInt32BigEndian(data, ref offset);

            long minDeltaBase;
            long firstValue;

            if (_dataType == TsDataType.Int32 || _dataType == TsDataType.Float)
            {
                minDeltaBase = ReadInt32BigEndian(data, ref offset);
                firstValue = ReadInt32BigEndian(data, ref offset);
            }
            else
            {
                minDeltaBase = ReadInt64BigEndian(data, ref offset);
                firstValue = ReadInt64BigEndian(data, ref offset);
            }

            int encodingLength = (int)Math.Ceiling((long)packNum * packWidth / 8.0);
            byte[] deltaBuf = new byte[encodingLength];
            if (encodingLength > 0)
                Array.Copy(data, offset, deltaBuf, 0, encodingLength);
            offset += encodingLength;

            // Reconstruct values AFTER firstValue (matching Java's readPack)
            _data = new long[packNum];
            long previous = firstValue;
            for (int i = 0; i < packNum; i++)
            {
                long v = BytesToLong(deltaBuf, (long)packWidth * i, packWidth);
                long current = previous + minDeltaBase + v;
                _data[i] = current;
                previous = current;
            }

            _readTotalCount = packNum;
            _nextReadIndex = 0;
            return firstValue;
        }

        /// <summary>
        /// Extract bits from byte array at bit position pos with given bit width.
        /// Matches Java BytesUtils.bytesToLong(byte[], int pos, int width).
        /// </summary>
        private static long BytesToLong(byte[] result, long pos, int width)
        {
            long ret = 0;
            int cnt = (int)(pos & 0x07);
            int index = (int)(pos >> 3);
            while (width > 0)
            {
                int m = (width + cnt >= 8) ? (8 - cnt) : width;
                width -= m;
                ret <<= m;
                byte y = (byte)(result[index] & (0xff >> cnt));
                y = (byte)((y & 0xff) >> (8 - cnt - m));
                ret |= (y & 0xffL);
                cnt += m;
                if (cnt == 8)
                {
                    cnt = 0;
                    index++;
                }
            }
            return ret;
        }

        private static int ReadInt32BigEndian(byte[] data, ref int offset)
        {
            int value = (data[offset] << 24)
                      | (data[offset + 1] << 16)
                      | (data[offset + 2] << 8)
                      | data[offset + 3];
            offset += 4;
            return value;
        }

        private static long ReadInt64BigEndian(byte[] data, ref int offset)
        {
            long value = ((long)data[offset] << 56)
                       | ((long)data[offset + 1] << 48)
                       | ((long)data[offset + 2] << 40)
                       | ((long)data[offset + 3] << 32)
                       | ((long)data[offset + 4] << 24)
                       | ((long)data[offset + 5] << 16)
                       | ((long)data[offset + 6] << 8)
                       | data[offset + 7];
            offset += 8;
            return value;
        }
    }
}
