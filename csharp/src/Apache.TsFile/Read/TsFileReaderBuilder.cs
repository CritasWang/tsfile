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

using Apache.TsFile.Encrypt;
using Apache.TsFile.IO;

namespace Apache.TsFile.Read;

/// <summary>
/// Builder for creating TsFileReader instances.
/// Provides a fluent API for configuring TsFile readers.
/// </summary>
public class TsFileReaderBuilder
{
    private string? _filePath;
    private FileInfo? _file;
    private EncryptParameter? _encryptParameter;
    
    /// <summary>
    /// Sets the file path for the TsFile reader.
    /// </summary>
    /// <param name="filePath">The path to the TsFile to read.</param>
    /// <returns>This builder for method chaining.</returns>
    public TsFileReaderBuilder FilePath(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _file = new FileInfo(filePath);
        return this;
    }
    
    /// <summary>
    /// Sets the file for the TsFile reader.
    /// </summary>
    /// <param name="file">The FileInfo for the TsFile to read.</param>
    /// <returns>This builder for method chaining.</returns>
    public TsFileReaderBuilder File(FileInfo file)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
        _filePath = file.FullName;
        return this;
    }
    
    /// <summary>
    /// Sets the encryption parameters for decryption.
    /// </summary>
    /// <param name="encryptParameter">The encryption parameters.</param>
    /// <returns>This builder for method chaining.</returns>
    public TsFileReaderBuilder WithEncryption(EncryptParameter encryptParameter)
    {
        _encryptParameter = encryptParameter;
        return this;
    }
    
    /// <summary>
    /// Builds and returns a TsFileReader instance.
    /// </summary>
    /// <returns>A new TsFileReader configured with the specified options.</returns>
    public TsFileReader Build()
    {
        ValidateParameters();
        
        // Note: Encryption is not yet implemented in TsFileReader.
        // The encryption parameter is stored for future implementation.
        if (_encryptParameter != null && 
            !_encryptParameter.Type.Equals("UNENCRYPTED", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotImplementedException(
                "Encryption is not yet implemented. Use 'UNENCRYPTED' or omit encryption configuration.");
        }
        
        return new TsFileReader(_filePath!);
    }
    
    private void ValidateParameters()
    {
        if (string.IsNullOrEmpty(_filePath) && _file == null)
        {
            throw new InvalidOperationException(
                "File path must be specified. Call FilePath() or File() before Build().");
        }
        
        if (_file != null && (!_file.Exists || _file.Attributes.HasFlag(FileAttributes.Directory)))
        {
            throw new ArgumentException(
                "The file must be a non-null and non-directory file that exists.");
        }
    }
}
