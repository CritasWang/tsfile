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
/// Interface for data encryption in TsFile.
/// </summary>
public interface IEncryptor
{
    /// <summary>
    /// Encrypts the given data.
    /// </summary>
    /// <param name="data">The data to encrypt.</param>
    /// <returns>The encrypted data.</returns>
    byte[] Encrypt(byte[] data);
    
    /// <summary>
    /// Encrypts a portion of the given data.
    /// </summary>
    /// <param name="data">The source data array.</param>
    /// <param name="offset">The offset in the source array.</param>
    /// <param name="size">The number of bytes to encrypt.</param>
    /// <returns>The encrypted data.</returns>
    byte[] Encrypt(byte[] data, int offset, int size);
    
    /// <summary>
    /// Gets the encryption type.
    /// </summary>
    EncryptionType EncryptionType { get; }
}

/// <summary>
/// Factory for creating encryptors.
/// </summary>
public static class EncryptorFactory
{
    /// <summary>
    /// Gets an encryptor for the specified encryption type and key.
    /// </summary>
    /// <param name="type">The encryption type name.</param>
    /// <param name="key">The encryption key.</param>
    /// <returns>An encryptor instance.</returns>
    public static IEncryptor GetEncryptor(string type, byte[] key)
    {
        if (string.IsNullOrEmpty(type) || 
            type.Equals("UNENCRYPTED", StringComparison.OrdinalIgnoreCase))
        {
            return new NoEncryptor();
        }
        
        // For other encryption types, throw NotImplementedException
        // This is a placeholder for future encryption implementations
        throw new NotImplementedException($"Encryption type '{type}' is not yet implemented. " +
            "Use 'UNENCRYPTED' or implement a custom IEncryptor.");
    }
    
    /// <summary>
    /// Gets an encryptor for the specified encryption parameter.
    /// </summary>
    /// <param name="encryptParam">The encryption parameter.</param>
    /// <returns>An encryptor instance.</returns>
    public static IEncryptor GetEncryptor(EncryptParameter encryptParam)
    {
        return GetEncryptor(encryptParam.Type, encryptParam.Key);
    }
}
