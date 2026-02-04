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
using Apache.TsFile.Read.Filter;

namespace Apache.TsFile.Read.Common;

/// <summary>
/// TsBlock is a columnar data container for query results.
/// It stores data in a column-oriented format for efficient access.
/// </summary>
public class TsBlock
{
    private readonly TimeColumn _timeColumn;
    private readonly IColumn[] _valueColumns;
    private readonly int _positionCount;
    
    /// <summary>
    /// Creates a new TsBlock with the specified columns.
    /// </summary>
    /// <param name="timeColumn">The time column.</param>
    /// <param name="valueColumns">The value columns.</param>
    public TsBlock(TimeColumn timeColumn, params IColumn[] valueColumns)
    {
        _timeColumn = timeColumn ?? throw new ArgumentNullException(nameof(timeColumn));
        _valueColumns = valueColumns ?? Array.Empty<IColumn>();
        _positionCount = timeColumn.PositionCount;
    }
    
    /// <summary>
    /// Gets the number of rows (positions) in this block.
    /// </summary>
    public int PositionCount => _positionCount;
    
    /// <summary>
    /// Gets the number of value columns in this block.
    /// </summary>
    public int ValueColumnCount => _valueColumns.Length;
    
    /// <summary>
    /// Gets the time column.
    /// </summary>
    public TimeColumn TimeColumn => _timeColumn;
    
    /// <summary>
    /// Gets a specific value column by index.
    /// </summary>
    /// <param name="index">The column index.</param>
    /// <returns>The column at the specified index.</returns>
    public IColumn GetColumn(int index)
    {
        if (index < 0 || index >= _valueColumns.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _valueColumns[index];
    }
    
    /// <summary>
    /// Gets the timestamp at the specified position.
    /// </summary>
    /// <param name="position">The row position.</param>
    /// <returns>The timestamp value.</returns>
    public long GetTime(int position) => _timeColumn.GetTime(position);
    
    /// <summary>
    /// Gets the start time (minimum timestamp) in this block.
    /// </summary>
    public long StartTime => _positionCount > 0 ? _timeColumn.GetTime(0) : 0;
    
    /// <summary>
    /// Gets the end time (maximum timestamp) in this block.
    /// </summary>
    public long EndTime => _positionCount > 0 ? _timeColumn.GetTime(_positionCount - 1) : 0;
    
    /// <summary>
    /// Gets whether this block is empty.
    /// </summary>
    public bool IsEmpty => _positionCount == 0;
}

/// <summary>
/// Interface for a column of values.
/// </summary>
public interface IColumn
{
    /// <summary>
    /// Gets the data type of this column.
    /// </summary>
    TsDataType DataType { get; }
    
    /// <summary>
    /// Gets the number of positions (rows) in this column.
    /// </summary>
    int PositionCount { get; }
    
    /// <summary>
    /// Gets whether the value at the specified position is null.
    /// </summary>
    bool IsNull(int position);
    
    /// <summary>
    /// Gets the value at the specified position as an object.
    /// </summary>
    object? GetObject(int position);
}

/// <summary>
/// Time column storing timestamps.
/// </summary>
public class TimeColumn
{
    private readonly long[] _times;
    private readonly int _positionCount;
    
    public TimeColumn(long[] times, int positionCount)
    {
        _times = times ?? throw new ArgumentNullException(nameof(times));
        _positionCount = positionCount;
    }
    
    public int PositionCount => _positionCount;
    
    public long GetTime(int position)
    {
        if (position < 0 || position >= _positionCount)
            throw new ArgumentOutOfRangeException(nameof(position));
        return _times[position];
    }
}

/// <summary>
/// Boolean value column.
/// </summary>
public class BooleanColumn : IColumn
{
    private readonly bool[] _values;
    private readonly bool[]? _nulls;
    private readonly int _positionCount;
    
    public BooleanColumn(bool[] values, bool[]? nulls, int positionCount)
    {
        _values = values;
        _nulls = nulls;
        _positionCount = positionCount;
    }
    
    public TsDataType DataType => TsDataType.Boolean;
    public int PositionCount => _positionCount;
    public bool IsNull(int position) => _nulls?[position] ?? false;
    public bool GetBoolean(int position) => _values[position];
    public object? GetObject(int position) => IsNull(position) ? null : _values[position];
}

/// <summary>
/// Int32 value column.
/// </summary>
public class IntColumn : IColumn
{
    private readonly int[] _values;
    private readonly bool[]? _nulls;
    private readonly int _positionCount;
    
    public IntColumn(int[] values, bool[]? nulls, int positionCount)
    {
        _values = values;
        _nulls = nulls;
        _positionCount = positionCount;
    }
    
    public TsDataType DataType => TsDataType.Int32;
    public int PositionCount => _positionCount;
    public bool IsNull(int position) => _nulls?[position] ?? false;
    public int GetInt(int position) => _values[position];
    public object? GetObject(int position) => IsNull(position) ? null : _values[position];
}

/// <summary>
/// Int64 value column.
/// </summary>
public class LongColumn : IColumn
{
    private readonly long[] _values;
    private readonly bool[]? _nulls;
    private readonly int _positionCount;
    
    public LongColumn(long[] values, bool[]? nulls, int positionCount)
    {
        _values = values;
        _nulls = nulls;
        _positionCount = positionCount;
    }
    
    public TsDataType DataType => TsDataType.Int64;
    public int PositionCount => _positionCount;
    public bool IsNull(int position) => _nulls?[position] ?? false;
    public long GetLong(int position) => _values[position];
    public object? GetObject(int position) => IsNull(position) ? null : _values[position];
}

/// <summary>
/// Float value column.
/// </summary>
public class FloatColumn : IColumn
{
    private readonly float[] _values;
    private readonly bool[]? _nulls;
    private readonly int _positionCount;
    
    public FloatColumn(float[] values, bool[]? nulls, int positionCount)
    {
        _values = values;
        _nulls = nulls;
        _positionCount = positionCount;
    }
    
    public TsDataType DataType => TsDataType.Float;
    public int PositionCount => _positionCount;
    public bool IsNull(int position) => _nulls?[position] ?? false;
    public float GetFloat(int position) => _values[position];
    public object? GetObject(int position) => IsNull(position) ? null : _values[position];
}

/// <summary>
/// Double value column.
/// </summary>
public class DoubleColumn : IColumn
{
    private readonly double[] _values;
    private readonly bool[]? _nulls;
    private readonly int _positionCount;
    
    public DoubleColumn(double[] values, bool[]? nulls, int positionCount)
    {
        _values = values;
        _nulls = nulls;
        _positionCount = positionCount;
    }
    
    public TsDataType DataType => TsDataType.Double;
    public int PositionCount => _positionCount;
    public bool IsNull(int position) => _nulls?[position] ?? false;
    public double GetDouble(int position) => _values[position];
    public object? GetObject(int position) => IsNull(position) ? null : _values[position];
}

/// <summary>
/// Binary (byte array) value column.
/// </summary>
public class BinaryColumn : IColumn
{
    private readonly byte[]?[] _values;
    private readonly int _positionCount;
    
    public BinaryColumn(byte[]?[] values, int positionCount)
    {
        _values = values;
        _positionCount = positionCount;
    }
    
    public TsDataType DataType => TsDataType.Blob;
    public int PositionCount => _positionCount;
    public bool IsNull(int position) => _values[position] == null;
    public byte[]? GetBinary(int position) => _values[position];
    public object? GetObject(int position) => _values[position];
}

/// <summary>
/// Builder for creating TsBlock instances.
/// </summary>
public class TsBlockBuilder
{
    private readonly List<TsDataType> _dataTypes;
    private readonly List<long> _times;
    private readonly List<List<object?>> _columns;
    private int _declaredPositions;
    
    public TsBlockBuilder(IEnumerable<TsDataType> dataTypes)
    {
        _dataTypes = dataTypes.ToList();
        _times = new List<long>();
        _columns = _dataTypes.Select(_ => new List<object?>()).ToList();
    }
    
    public TsBlockBuilder(int initialCapacity, IEnumerable<TsDataType> dataTypes)
    {
        _dataTypes = dataTypes.ToList();
        _times = new List<long>(initialCapacity);
        _columns = _dataTypes.Select(_ => new List<object?>(initialCapacity)).ToList();
    }
    
    /// <summary>
    /// Gets the time column builder.
    /// </summary>
    public TimeColumnBuilder GetTimeColumnBuilder() => new TimeColumnBuilder(_times);
    
    /// <summary>
    /// Gets the column builder for the specified column index.
    /// </summary>
    public ColumnBuilder GetColumnBuilder(int index) => new ColumnBuilder(_columns[index], _dataTypes[index]);
    
    /// <summary>
    /// Declares a new position (row) in the block.
    /// </summary>
    public void DeclarePosition()
    {
        _declaredPositions++;
    }
    
    /// <summary>
    /// Builds the TsBlock from the accumulated data.
    /// </summary>
    public TsBlock Build()
    {
        var timeColumn = new TimeColumn(_times.ToArray(), _declaredPositions);
        var valueColumns = _columns.Select((col, i) => BuildColumn(col, _dataTypes[i], _declaredPositions)).ToArray();
        return new TsBlock(timeColumn, valueColumns);
    }
    
    private static IColumn BuildColumn(List<object?> values, TsDataType dataType, int positionCount)
    {
        switch (dataType)
        {
            case TsDataType.Boolean:
                return new BooleanColumn(
                    values.Select(v => v is bool b && b).ToArray(),
                    values.Select(v => v == null).ToArray(),
                    positionCount);
            case TsDataType.Int32:
            case TsDataType.Date:
                return new IntColumn(
                    values.Select(v => v is int i ? i : 0).ToArray(),
                    values.Select(v => v == null).ToArray(),
                    positionCount);
            case TsDataType.Int64:
            case TsDataType.Timestamp:
                return new LongColumn(
                    values.Select(v => v is long l ? l : 0).ToArray(),
                    values.Select(v => v == null).ToArray(),
                    positionCount);
            case TsDataType.Float:
                return new FloatColumn(
                    values.Select(v => v is float f ? f : 0).ToArray(),
                    values.Select(v => v == null).ToArray(),
                    positionCount);
            case TsDataType.Double:
                return new DoubleColumn(
                    values.Select(v => v is double d ? d : 0).ToArray(),
                    values.Select(v => v == null).ToArray(),
                    positionCount);
            default:
                return new BinaryColumn(
                    values.Select(v => v as byte[]).ToArray(),
                    positionCount);
        }
    }
}

/// <summary>
/// Builder for time column.
/// </summary>
public class TimeColumnBuilder
{
    private readonly List<long> _times;
    
    public TimeColumnBuilder(List<long> times)
    {
        _times = times;
    }
    
    public void WriteLong(long value)
    {
        _times.Add(value);
    }
}

/// <summary>
/// Builder for value columns.
/// </summary>
public class ColumnBuilder
{
    private readonly List<object?> _values;
    private readonly TsDataType _dataType;
    
    public ColumnBuilder(List<object?> values, TsDataType dataType)
    {
        _values = values;
        _dataType = dataType;
    }
    
    public void WriteBoolean(bool value) => _values.Add(value);
    public void WriteInt(int value) => _values.Add(value);
    public void WriteLong(long value) => _values.Add(value);
    public void WriteFloat(float value) => _values.Add(value);
    public void WriteDouble(double value) => _values.Add(value);
    public void WriteBinary(byte[]? value) => _values.Add(value);
    public void WriteNull() => _values.Add(null);
}
