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
using Apache.TsFile.Schema;

namespace Apache.TsFile.Write;

/// <summary>
/// Builder for creating TsFileWriterV4 instances.
/// Provides a fluent API for configuring TsFile v4 writers.
/// </summary>
public class TsFileWriterV4Builder
{
    private string? _filePath;
    private FileInfo? _file;
    private EncryptParameter? _encryptParameter;
    private TableSchema? _tableSchema;
    
    /// <summary>
    /// Sets the file path for the TsFile writer.
    /// </summary>
    /// <param name="filePath">The path to the TsFile to write.</param>
    /// <returns>This builder for method chaining.</returns>
    public TsFileWriterV4Builder FilePath(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _file = new FileInfo(filePath);
        return this;
    }
    
    /// <summary>
    /// Sets the file for the TsFile writer.
    /// </summary>
    /// <param name="file">The FileInfo for the TsFile to write.</param>
    /// <returns>This builder for method chaining.</returns>
    public TsFileWriterV4Builder File(FileInfo file)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
        _filePath = file.FullName;
        return this;
    }
    
    /// <summary>
    /// Sets the table schema for the writer.
    /// </summary>
    /// <param name="tableSchema">The table schema to use.</param>
    /// <returns>This builder for method chaining.</returns>
    public TsFileWriterV4Builder WithTableSchema(TableSchema tableSchema)
    {
        _tableSchema = tableSchema ?? throw new ArgumentNullException(nameof(tableSchema));
        return this;
    }
    
    /// <summary>
    /// Sets the encryption parameters.
    /// </summary>
    /// <param name="encryptParameter">The encryption parameters.</param>
    /// <returns>This builder for method chaining.</returns>
    public TsFileWriterV4Builder WithEncryption(EncryptParameter encryptParameter)
    {
        _encryptParameter = encryptParameter;
        return this;
    }
    
    /// <summary>
    /// Builds and returns a TsFileWriterV4 instance.
    /// </summary>
    /// <returns>A new TsFileWriterV4 configured with the specified options.</returns>
    public TsFileWriterV4 Build()
    {
        ValidateParameters();
        
        // Note: Encryption is not yet implemented in TsFileWriterV4.
        // The encryption parameter is stored for future implementation.
        if (_encryptParameter != null && 
            !_encryptParameter.Type.Equals("UNENCRYPTED", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotImplementedException(
                "Encryption is not yet implemented. Use 'UNENCRYPTED' or omit encryption configuration.");
        }
        
        return new TsFileWriterV4(_filePath!, _tableSchema!);
    }
    
    private void ValidateParameters()
    {
        if (string.IsNullOrEmpty(_filePath) && _file == null)
        {
            throw new InvalidOperationException(
                "File path must be specified. Call FilePath() or File() before Build().");
        }
        
        if (_tableSchema == null)
        {
            throw new InvalidOperationException(
                "Table schema must be specified. Call WithTableSchema() before Build().");
        }
    }
}
