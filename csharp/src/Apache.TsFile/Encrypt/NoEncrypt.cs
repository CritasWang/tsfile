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

namespace Apache.TsFile.Encrypt;

/// <summary>
/// No-op encryptor that returns data unchanged.
/// Used when encryption is not enabled.
/// </summary>
public sealed class NoEncryptor : IEncryptor
{
    /// <inheritdoc />
    public EncryptionType EncryptionType => EncryptionType.Unencrypted;
    
    /// <inheritdoc />
    public byte[] Encrypt(byte[] data)
    {
        return data;
    }
    
    /// <inheritdoc />
    public byte[] Encrypt(byte[] data, int offset, int size)
    {
        if (offset == 0 && size == data.Length)
        {
            return data;
        }
        
        var result = new byte[size];
        Array.Copy(data, offset, result, 0, size);
        return result;
    }
}

/// <summary>
/// No-op decryptor that returns data unchanged.
/// Used when encryption is not enabled.
/// </summary>
public sealed class NoDecryptor : IDecryptor
{
    /// <inheritdoc />
    public EncryptionType EncryptionType => EncryptionType.Unencrypted;
    
    /// <inheritdoc />
    public byte[] Decrypt(byte[] data)
    {
        return data;
    }
    
    /// <inheritdoc />
    public byte[] Decrypt(byte[] data, int offset, int size)
    {
        if (offset == 0 && size == data.Length)
        {
            return data;
        }
        
        var result = new byte[size];
        Array.Copy(data, offset, result, 0, size);
        return result;
    }
}
