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

namespace Apache.TsFile.Compress;

/// <summary>
/// LZMA2 compressor implementation.
/// Note: LZMA2 is not yet fully implemented due to lack of compatible .NET 10 library.
/// Java uses org.tukaani.xz (XZ format with LZMA2 algorithm).
/// Available C# libraries either don't support .NET 10 or only support decompression.
/// Use ZSTD, LZ4, or GZIP compression instead for production use.
/// </summary>
public class Lzma2Compressor : ICompressor, IUncompressor
{
    public CompressionType Type => CompressionType.Lzma2;
    
    public byte[] Compress(byte[] data)
    {
        throw new NotSupportedException(
            "LZMA2 compression is not yet implemented. " +
            "Use ZSTD (recommended), LZ4 (fast), or GZIP (widely compatible) instead.");
    }
    
    public byte[] Compress(byte[] data, int offset, int length)
    {
        throw new NotSupportedException(
            "LZMA2 compression is not yet implemented. " +
            "Use ZSTD (recommended), LZ4 (fast), or GZIP (widely compatible) instead.");
    }
    
    public int Compress(byte[] data, int offset, int length, byte[] compressed)
    {
        throw new NotSupportedException(
            "LZMA2 compression is not yet implemented. " +
            "Use ZSTD (recommended), LZ4 (fast), or GZIP (widely compatible) instead.");
    }
    
    public int GetMaxCompressedSize(int uncompressedSize)
    {
        // Conservative estimate for XZ/LZMA2 format
        return 100 + uncompressedSize;
    }
    
    public byte[] Uncompress(byte[] data)
    {
        throw new NotSupportedException(
            "LZMA2 decompression is not yet implemented. " +
            "Use ZSTD (recommended), LZ4 (fast), or GZIP (widely compatible) instead.");
    }
    
    public byte[] Uncompress(byte[] data, int offset, int length)
    {
        throw new NotSupportedException(
            "LZMA2 decompression is not yet implemented. " +
            "Use ZSTD (recommended), LZ4 (fast), or GZIP (widely compatible) instead.");
    }
    
    public int Uncompress(byte[] data, int offset, int length, byte[] output, int outputOffset)
    {
        throw new NotSupportedException(
            "LZMA2 decompression is not yet implemented. " +
            "Use ZSTD (recommended), LZ4 (fast), or GZIP (widely compatible) instead.");
    }
}
