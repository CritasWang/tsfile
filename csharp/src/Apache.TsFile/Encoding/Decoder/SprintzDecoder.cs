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
/// SPRINTZ decoder - Sensor-optimized decompression.
/// </summary>
public class SprintzDecoder : IDecoder
{
    private readonly PlainDecoder _fallbackDecoder = new();
    
    public SprintzDecoder(TsDataType dataType)
    {
        // For now, fall back to Plain decoding
    }
    
    public bool ReadBoolean(byte[] buffer, ref int offset)
    {
        return _fallbackDecoder.ReadBoolean(buffer, ref offset);
    }
    
    public int ReadInt(byte[] buffer, ref int offset)
    {
        return _fallbackDecoder.ReadInt(buffer, ref offset);
    }
    
    public long ReadLong(byte[] buffer, ref int offset)
    {
        return _fallbackDecoder.ReadLong(buffer, ref offset);
    }
    
    public float ReadFloat(byte[] buffer, ref int offset)
    {
        return _fallbackDecoder.ReadFloat(buffer, ref offset);
    }
    
    public double ReadDouble(byte[] buffer, ref int offset)
    {
        return _fallbackDecoder.ReadDouble(buffer, ref offset);
    }
    
    public string ReadString(byte[] buffer, ref int offset)
    {
        return _fallbackDecoder.ReadString(buffer, ref offset);
    }
    
    public byte[] ReadBytes(byte[] buffer, ref int offset)
    {
        return _fallbackDecoder.ReadBytes(buffer, ref offset);
    }
    
    public bool HasNext(byte[] buffer, int offset)
    {
        return _fallbackDecoder.HasNext(buffer, offset);
    }
    
    public void Reset()
    {
        _fallbackDecoder.Reset();
    }
}
