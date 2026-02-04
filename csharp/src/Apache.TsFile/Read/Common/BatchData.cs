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

namespace Apache.TsFile.Read.Common;

/// <summary>
/// BatchData is a self-defined data structure optimized for different types of values.
/// This class records a time list and a value list for efficient iteration.
/// </summary>
public class BatchData
{
    private const int DefaultCapacity = 16;
    private const int CapacityThreshold = 1024;
    
    private readonly TsDataType _dataType;
    private int _capacity;
    
    // Time data
    private List<long[]> _timeArrays;
    
    // Value data (only one list is used based on data type)
    private List<bool[]>? _booleanArrays;
    private List<int[]>? _intArrays;
    private List<long[]>? _longArrays;
    private List<float[]>? _floatArrays;
    private List<double[]>? _doubleArrays;
    private List<byte[]?[]>? _binaryArrays;
    private List<string?[]>? _stringArrays;
    
    // Read position
    private int _readListIndex;
    private int _readArrayIndex;
    
    // Write position
    private int _writeListIndex;
    private int _writeArrayIndex;
    
    // Total count
    private int _count;
    
    /// <summary>
    /// Creates a new BatchData for the specified data type.
    /// </summary>
    /// <param name="dataType">The data type of values in this batch.</param>
    public BatchData(TsDataType dataType)
    {
        _dataType = dataType;
        _capacity = DefaultCapacity;
        _timeArrays = new List<long[]> { new long[_capacity] };
        
        InitializeValueArrays(dataType);
    }
    
    /// <summary>
    /// Gets the data type of this batch.
    /// </summary>
    public TsDataType DataType => _dataType;
    
    /// <summary>
    /// Gets the number of data points in this batch.
    /// </summary>
    public int Count => _count;
    
    /// <summary>
    /// Gets whether the batch is empty.
    /// </summary>
    public bool IsEmpty => _count == 0;
    
    /// <summary>
    /// Gets whether there is more data to read.
    /// </summary>
    public bool HasCurrent
    {
        get
        {
            if (_readListIndex == _writeListIndex)
                return _readArrayIndex < _writeArrayIndex;
            return _readListIndex < _writeListIndex && _readArrayIndex < _capacity;
        }
    }
    
    /// <summary>
    /// Moves to the next data point.
    /// </summary>
    public void Next()
    {
        _readArrayIndex++;
        if (_readArrayIndex == _capacity)
        {
            _readArrayIndex = 0;
            _readListIndex++;
        }
    }
    
    /// <summary>
    /// Gets the timestamp at the current read position.
    /// </summary>
    public long CurrentTime => _timeArrays[_readListIndex][_readArrayIndex];
    
    /// <summary>
    /// Gets the value at the current read position.
    /// </summary>
    public object? CurrentValue
    {
        get
        {
            return _dataType switch
            {
                TsDataType.Boolean => GetBoolean(),
                TsDataType.Int32 or TsDataType.Date => GetInt(),
                TsDataType.Int64 or TsDataType.Timestamp => GetLong(),
                TsDataType.Float => GetFloat(),
                TsDataType.Double => GetDouble(),
                TsDataType.Text or TsDataType.String => GetString(),
                TsDataType.Blob => GetBinary(),
                _ => null
            };
        }
    }
    
    /// <summary>
    /// Resets the read position to the beginning.
    /// </summary>
    public void ResetBatchData()
    {
        _readListIndex = 0;
        _readArrayIndex = 0;
    }
    
    /// <summary>
    /// Flips the batch data (prepares for reading after writing).
    /// </summary>
    public BatchData Flip()
    {
        return this;
    }
    
    #region Put Methods
    
    public void PutBoolean(long timestamp, bool value)
    {
        EnsureCapacity();
        _timeArrays[_writeListIndex][_writeArrayIndex] = timestamp;
        _booleanArrays![_writeListIndex][_writeArrayIndex] = value;
        IncrementWrite();
    }
    
    public void PutInt(long timestamp, int value)
    {
        EnsureCapacity();
        _timeArrays[_writeListIndex][_writeArrayIndex] = timestamp;
        _intArrays![_writeListIndex][_writeArrayIndex] = value;
        IncrementWrite();
    }
    
    public void PutLong(long timestamp, long value)
    {
        EnsureCapacity();
        _timeArrays[_writeListIndex][_writeArrayIndex] = timestamp;
        _longArrays![_writeListIndex][_writeArrayIndex] = value;
        IncrementWrite();
    }
    
    public void PutFloat(long timestamp, float value)
    {
        EnsureCapacity();
        _timeArrays[_writeListIndex][_writeArrayIndex] = timestamp;
        _floatArrays![_writeListIndex][_writeArrayIndex] = value;
        IncrementWrite();
    }
    
    public void PutDouble(long timestamp, double value)
    {
        EnsureCapacity();
        _timeArrays[_writeListIndex][_writeArrayIndex] = timestamp;
        _doubleArrays![_writeListIndex][_writeArrayIndex] = value;
        IncrementWrite();
    }
    
    public void PutString(long timestamp, string? value)
    {
        EnsureCapacity();
        _timeArrays[_writeListIndex][_writeArrayIndex] = timestamp;
        _stringArrays![_writeListIndex][_writeArrayIndex] = value;
        IncrementWrite();
    }
    
    public void PutBinary(long timestamp, byte[]? value)
    {
        EnsureCapacity();
        _timeArrays[_writeListIndex][_writeArrayIndex] = timestamp;
        _binaryArrays![_writeListIndex][_writeArrayIndex] = value;
        IncrementWrite();
    }
    
    public void PutValue(long timestamp, object? value)
    {
        switch (_dataType)
        {
            case TsDataType.Boolean:
                PutBoolean(timestamp, (bool)value!);
                break;
            case TsDataType.Int32:
            case TsDataType.Date:
                PutInt(timestamp, (int)value!);
                break;
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                PutLong(timestamp, (long)value!);
                break;
            case TsDataType.Float:
                PutFloat(timestamp, (float)value!);
                break;
            case TsDataType.Double:
                PutDouble(timestamp, (double)value!);
                break;
            case TsDataType.Text:
            case TsDataType.String:
                PutString(timestamp, value as string);
                break;
            case TsDataType.Blob:
                PutBinary(timestamp, value as byte[]);
                break;
        }
    }
    
    #endregion
    
    #region Get Methods
    
    public bool GetBoolean() => _booleanArrays![_readListIndex][_readArrayIndex];
    
    public int GetInt() => _intArrays![_readListIndex][_readArrayIndex];
    
    public long GetLong() => _longArrays![_readListIndex][_readArrayIndex];
    
    public float GetFloat() => _floatArrays![_readListIndex][_readArrayIndex];
    
    public double GetDouble() => _doubleArrays![_readListIndex][_readArrayIndex];
    
    public string? GetString() => _stringArrays![_readListIndex][_readArrayIndex];
    
    public byte[]? GetBinary() => _binaryArrays![_readListIndex][_readArrayIndex];
    
    #endregion
    
    #region Index-based Get Methods
    
    public long GetTimeByIndex(int index)
    {
        return _timeArrays[index / _capacity][index % _capacity];
    }
    
    public bool GetBooleanByIndex(int index) => _booleanArrays![index / _capacity][index % _capacity];
    
    public int GetIntByIndex(int index) => _intArrays![index / _capacity][index % _capacity];
    
    public long GetLongByIndex(int index) => _longArrays![index / _capacity][index % _capacity];
    
    public float GetFloatByIndex(int index) => _floatArrays![index / _capacity][index % _capacity];
    
    public double GetDoubleByIndex(int index) => _doubleArrays![index / _capacity][index % _capacity];
    
    public string? GetStringByIndex(int index) => _stringArrays![index / _capacity][index % _capacity];
    
    public byte[]? GetBinaryByIndex(int index) => _binaryArrays![index / _capacity][index % _capacity];
    
    public object? GetValueByIndex(int index)
    {
        return _dataType switch
        {
            TsDataType.Boolean => GetBooleanByIndex(index),
            TsDataType.Int32 or TsDataType.Date => GetIntByIndex(index),
            TsDataType.Int64 or TsDataType.Timestamp => GetLongByIndex(index),
            TsDataType.Float => GetFloatByIndex(index),
            TsDataType.Double => GetDoubleByIndex(index),
            TsDataType.Text or TsDataType.String => GetStringByIndex(index),
            TsDataType.Blob => GetBinaryByIndex(index),
            _ => null
        };
    }
    
    #endregion
    
    #region Statistics
    
    public long GetMinTimestamp() => _count > 0 ? GetTimeByIndex(0) : 0;
    
    public long GetMaxTimestamp() => _count > 0 ? GetTimeByIndex(_count - 1) : 0;
    
    #endregion
    
    #region Private Methods
    
    private void InitializeValueArrays(TsDataType dataType)
    {
        switch (dataType)
        {
            case TsDataType.Boolean:
                _booleanArrays = new List<bool[]> { new bool[_capacity] };
                break;
            case TsDataType.Int32:
            case TsDataType.Date:
                _intArrays = new List<int[]> { new int[_capacity] };
                break;
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                _longArrays = new List<long[]> { new long[_capacity] };
                break;
            case TsDataType.Float:
                _floatArrays = new List<float[]> { new float[_capacity] };
                break;
            case TsDataType.Double:
                _doubleArrays = new List<double[]> { new double[_capacity] };
                break;
            case TsDataType.Text:
            case TsDataType.String:
                _stringArrays = new List<string?[]> { new string?[_capacity] };
                break;
            case TsDataType.Blob:
                _binaryArrays = new List<byte[]?[]> { new byte[]?[_capacity] };
                break;
        }
    }
    
    private void EnsureCapacity()
    {
        if (_writeArrayIndex == _capacity)
        {
            if (_capacity >= CapacityThreshold)
            {
                // Add new arrays
                _timeArrays.Add(new long[_capacity]);
                AddNewValueArray();
                _writeListIndex++;
                _writeArrayIndex = 0;
            }
            else
            {
                // Double capacity
                int newCapacity = _capacity << 1;
                ExpandArrays(newCapacity);
                _capacity = newCapacity;
            }
        }
    }
    
    private void AddNewValueArray()
    {
        switch (_dataType)
        {
            case TsDataType.Boolean:
                _booleanArrays!.Add(new bool[_capacity]);
                break;
            case TsDataType.Int32:
            case TsDataType.Date:
                _intArrays!.Add(new int[_capacity]);
                break;
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                _longArrays!.Add(new long[_capacity]);
                break;
            case TsDataType.Float:
                _floatArrays!.Add(new float[_capacity]);
                break;
            case TsDataType.Double:
                _doubleArrays!.Add(new double[_capacity]);
                break;
            case TsDataType.Text:
            case TsDataType.String:
                _stringArrays!.Add(new string?[_capacity]);
                break;
            case TsDataType.Blob:
                _binaryArrays!.Add(new byte[]?[_capacity]);
                break;
        }
    }
    
    private void ExpandArrays(int newCapacity)
    {
        // Expand time array
        var newTimeArray = new long[newCapacity];
        Array.Copy(_timeArrays[0], newTimeArray, _capacity);
        _timeArrays[0] = newTimeArray;
        
        // Expand value array based on type
        switch (_dataType)
        {
            case TsDataType.Boolean:
                var newBoolArray = new bool[newCapacity];
                Array.Copy(_booleanArrays![0], newBoolArray, _capacity);
                _booleanArrays[0] = newBoolArray;
                break;
            case TsDataType.Int32:
            case TsDataType.Date:
                var newIntArray = new int[newCapacity];
                Array.Copy(_intArrays![0], newIntArray, _capacity);
                _intArrays[0] = newIntArray;
                break;
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                var newLongArray = new long[newCapacity];
                Array.Copy(_longArrays![0], newLongArray, _capacity);
                _longArrays[0] = newLongArray;
                break;
            case TsDataType.Float:
                var newFloatArray = new float[newCapacity];
                Array.Copy(_floatArrays![0], newFloatArray, _capacity);
                _floatArrays[0] = newFloatArray;
                break;
            case TsDataType.Double:
                var newDoubleArray = new double[newCapacity];
                Array.Copy(_doubleArrays![0], newDoubleArray, _capacity);
                _doubleArrays[0] = newDoubleArray;
                break;
            case TsDataType.Text:
            case TsDataType.String:
                var newStringArray = new string?[newCapacity];
                Array.Copy(_stringArrays![0], newStringArray, _capacity);
                _stringArrays[0] = newStringArray;
                break;
            case TsDataType.Blob:
                var newBinaryArray = new byte[]?[newCapacity];
                Array.Copy(_binaryArrays![0], newBinaryArray, _capacity);
                _binaryArrays[0] = newBinaryArray;
                break;
        }
    }
    
    private void IncrementWrite()
    {
        _writeArrayIndex++;
        _count++;
    }
    
    #endregion
    
    /// <summary>
    /// Creates batch data for the specified data type.
    /// </summary>
    public static BatchData Create(TsDataType dataType, bool ascending = true)
    {
        return new BatchData(dataType);
    }
}
