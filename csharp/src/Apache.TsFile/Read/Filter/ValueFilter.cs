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

namespace Apache.TsFile.Read.Filter;

/// <summary>
/// Value filter operators for filtering data based on values.
/// </summary>
public abstract class ValueFilter : Filter
{
    /// <summary>
    /// Gets the column index this filter applies to (0 for single column queries).
    /// </summary>
    public int ColumnIndex { get; }
    
    protected ValueFilter(int columnIndex = 0)
    {
        ColumnIndex = columnIndex;
    }
    
    /// <summary>
    /// Creates a filter for values equal to the specified value.
    /// </summary>
    public static ValueFilter Eq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => new ValueEqFilter<T>(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values not equal to the specified value.
    /// </summary>
    public static ValueFilter NotEq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => new ValueNotEqFilter<T>(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values greater than the specified value.
    /// </summary>
    public static ValueFilter Gt<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => new ValueGtFilter<T>(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values greater than or equal to the specified value.
    /// </summary>
    public static ValueFilter GtEq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => new ValueGtEqFilter<T>(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values less than the specified value.
    /// </summary>
    public static ValueFilter Lt<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => new ValueLtFilter<T>(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values less than or equal to the specified value.
    /// </summary>
    public static ValueFilter LtEq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => new ValueLtEqFilter<T>(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values between the specified values (inclusive).
    /// </summary>
    public static ValueFilter Between<T>(T minValue, T maxValue, int columnIndex = 0) where T : IComparable<T>
        => new ValueBetweenFilter<T>(minValue, maxValue, columnIndex);
    
    /// <summary>
    /// Creates a filter for null values.
    /// </summary>
    public static ValueFilter IsNull(int columnIndex = 0) => new ValueIsNullFilter(columnIndex);
    
    /// <summary>
    /// Creates a filter for non-null values.
    /// </summary>
    public static ValueFilter IsNotNull(int columnIndex = 0) => new ValueIsNotNullFilter(columnIndex);
    
    /// <inheritdoc />
    public override bool SatisfyStartEndTime(long startTime, long endTime) => true;
    
    /// <inheritdoc />
    public override bool ContainStartEndTime(long startTime, long endTime) => false;
    
    /// <inheritdoc />
    public override IReadOnlyList<TimeRange> GetTimeRanges() => 
        new[] { new TimeRange(long.MinValue, long.MaxValue) };
}

/// <summary>
/// Value equals filter.
/// </summary>
public sealed class ValueEqFilter<T> : ValueFilter where T : IComparable<T>
{
    private readonly T _value;
    
    public ValueEqFilter(T value, int columnIndex = 0) : base(columnIndex)
    {
        _value = value;
    }
    
    public override OperatorType OperatorType => OperatorType.ValueEq;
    
    public override bool Satisfy(long time, object? value) => 
        value != null && _value.CompareTo((T)value) == 0;
    
    public override bool SatisfyBoolean(long time, bool value) => 
        _value is bool b && b == value;
    
    public override bool SatisfyInteger(long time, int value) => 
        _value is int i && i == value;
    
    public override bool SatisfyLong(long time, long value) => 
        _value is long l && l == value;
    
    public override bool SatisfyFloat(long time, float value) => 
        _value is float f && Math.Abs(f - value) < float.Epsilon;
    
    public override bool SatisfyDouble(long time, double value) => 
        _value is double d && Math.Abs(d - value) < double.Epsilon;
    
    public override bool SatisfyString(long time, string? value) => 
        _value is string s && s == value;
    
    public override bool SatisfyBinary(long time, byte[]? value) => 
        _value is byte[] b && value != null && b.SequenceEqual(value);
    
    public override Filter Reverse() => new ValueNotEqFilter<T>(_value, ColumnIndex);
}

/// <summary>
/// Value not equals filter.
/// </summary>
public sealed class ValueNotEqFilter<T> : ValueFilter where T : IComparable<T>
{
    private readonly T _value;
    
    public ValueNotEqFilter(T value, int columnIndex = 0) : base(columnIndex)
    {
        _value = value;
    }
    
    public override OperatorType OperatorType => OperatorType.ValueNotEq;
    
    public override bool Satisfy(long time, object? value) => 
        value == null || _value.CompareTo((T)value) != 0;
    
    public override bool SatisfyBoolean(long time, bool value) => 
        !(_value is bool b && b == value);
    
    public override bool SatisfyInteger(long time, int value) => 
        !(_value is int i && i == value);
    
    public override bool SatisfyLong(long time, long value) => 
        !(_value is long l && l == value);
    
    public override bool SatisfyFloat(long time, float value) => 
        !(_value is float f && Math.Abs(f - value) < float.Epsilon);
    
    public override bool SatisfyDouble(long time, double value) => 
        !(_value is double d && Math.Abs(d - value) < double.Epsilon);
    
    public override bool SatisfyString(long time, string? value) => 
        !(_value is string s && s == value);
    
    public override bool SatisfyBinary(long time, byte[]? value) => 
        !(_value is byte[] b && value != null && b.SequenceEqual(value));
    
    public override Filter Reverse() => new ValueEqFilter<T>(_value, ColumnIndex);
}

/// <summary>
/// Value greater than filter.
/// </summary>
public sealed class ValueGtFilter<T> : ValueFilter where T : IComparable<T>
{
    private readonly T _value;
    
    public ValueGtFilter(T value, int columnIndex = 0) : base(columnIndex)
    {
        _value = value;
    }
    
    public override OperatorType OperatorType => OperatorType.ValueGt;
    
    public override bool Satisfy(long time, object? value) => 
        value != null && _value.CompareTo((T)value) < 0;
    
    public override bool SatisfyBoolean(long time, bool value) => false;
    
    public override bool SatisfyInteger(long time, int value) => 
        _value is int i && value > i;
    
    public override bool SatisfyLong(long time, long value) => 
        _value is long l && value > l;
    
    public override bool SatisfyFloat(long time, float value) => 
        _value is float f && value > f;
    
    public override bool SatisfyDouble(long time, double value) => 
        _value is double d && value > d;
    
    public override bool SatisfyString(long time, string? value) => 
        _value is string s && value != null && string.CompareOrdinal(value, s) > 0;
    
    public override bool SatisfyBinary(long time, byte[]? value) => false;
    
    public override Filter Reverse() => new ValueLtEqFilter<T>(_value, ColumnIndex);
}

/// <summary>
/// Value greater than or equals filter.
/// </summary>
public sealed class ValueGtEqFilter<T> : ValueFilter where T : IComparable<T>
{
    private readonly T _value;
    
    public ValueGtEqFilter(T value, int columnIndex = 0) : base(columnIndex)
    {
        _value = value;
    }
    
    public override OperatorType OperatorType => OperatorType.ValueGtEq;
    
    public override bool Satisfy(long time, object? value) => 
        value != null && _value.CompareTo((T)value) <= 0;
    
    public override bool SatisfyBoolean(long time, bool value) => 
        _value is bool b && (value || !b);
    
    public override bool SatisfyInteger(long time, int value) => 
        _value is int i && value >= i;
    
    public override bool SatisfyLong(long time, long value) => 
        _value is long l && value >= l;
    
    public override bool SatisfyFloat(long time, float value) => 
        _value is float f && value >= f;
    
    public override bool SatisfyDouble(long time, double value) => 
        _value is double d && value >= d;
    
    public override bool SatisfyString(long time, string? value) => 
        _value is string s && value != null && string.CompareOrdinal(value, s) >= 0;
    
    public override bool SatisfyBinary(long time, byte[]? value) => false;
    
    public override Filter Reverse() => new ValueLtFilter<T>(_value, ColumnIndex);
}

/// <summary>
/// Value less than filter.
/// </summary>
public sealed class ValueLtFilter<T> : ValueFilter where T : IComparable<T>
{
    private readonly T _value;
    
    public ValueLtFilter(T value, int columnIndex = 0) : base(columnIndex)
    {
        _value = value;
    }
    
    public override OperatorType OperatorType => OperatorType.ValueLt;
    
    public override bool Satisfy(long time, object? value) => 
        value != null && _value.CompareTo((T)value) > 0;
    
    public override bool SatisfyBoolean(long time, bool value) => false;
    
    public override bool SatisfyInteger(long time, int value) => 
        _value is int i && value < i;
    
    public override bool SatisfyLong(long time, long value) => 
        _value is long l && value < l;
    
    public override bool SatisfyFloat(long time, float value) => 
        _value is float f && value < f;
    
    public override bool SatisfyDouble(long time, double value) => 
        _value is double d && value < d;
    
    public override bool SatisfyString(long time, string? value) => 
        _value is string s && value != null && string.CompareOrdinal(value, s) < 0;
    
    public override bool SatisfyBinary(long time, byte[]? value) => false;
    
    public override Filter Reverse() => new ValueGtEqFilter<T>(_value, ColumnIndex);
}

/// <summary>
/// Value less than or equals filter.
/// </summary>
public sealed class ValueLtEqFilter<T> : ValueFilter where T : IComparable<T>
{
    private readonly T _value;
    
    public ValueLtEqFilter(T value, int columnIndex = 0) : base(columnIndex)
    {
        _value = value;
    }
    
    public override OperatorType OperatorType => OperatorType.ValueLtEq;
    
    public override bool Satisfy(long time, object? value) => 
        value != null && _value.CompareTo((T)value) >= 0;
    
    public override bool SatisfyBoolean(long time, bool value) => 
        _value is bool b && (!value || b);
    
    public override bool SatisfyInteger(long time, int value) => 
        _value is int i && value <= i;
    
    public override bool SatisfyLong(long time, long value) => 
        _value is long l && value <= l;
    
    public override bool SatisfyFloat(long time, float value) => 
        _value is float f && value <= f;
    
    public override bool SatisfyDouble(long time, double value) => 
        _value is double d && value <= d;
    
    public override bool SatisfyString(long time, string? value) => 
        _value is string s && value != null && string.CompareOrdinal(value, s) <= 0;
    
    public override bool SatisfyBinary(long time, byte[]? value) => false;
    
    public override Filter Reverse() => new ValueGtFilter<T>(_value, ColumnIndex);
}

/// <summary>
/// Value between filter (inclusive).
/// </summary>
public sealed class ValueBetweenFilter<T> : ValueFilter where T : IComparable<T>
{
    private readonly T _minValue;
    private readonly T _maxValue;
    
    public ValueBetweenFilter(T minValue, T maxValue, int columnIndex = 0) : base(columnIndex)
    {
        _minValue = minValue;
        _maxValue = maxValue;
    }
    
    public override OperatorType OperatorType => OperatorType.ValueBetweenAnd;
    
    public override bool Satisfy(long time, object? value) => 
        value != null && _minValue.CompareTo((T)value) <= 0 && _maxValue.CompareTo((T)value) >= 0;
    
    public override bool SatisfyBoolean(long time, bool value) => true;
    
    public override bool SatisfyInteger(long time, int value) => 
        _minValue is int min && _maxValue is int max && value >= min && value <= max;
    
    public override bool SatisfyLong(long time, long value) => 
        _minValue is long min && _maxValue is long max && value >= min && value <= max;
    
    public override bool SatisfyFloat(long time, float value) => 
        _minValue is float min && _maxValue is float max && value >= min && value <= max;
    
    public override bool SatisfyDouble(long time, double value) => 
        _minValue is double min && _maxValue is double max && value >= min && value <= max;
    
    public override bool SatisfyString(long time, string? value) => 
        _minValue is string min && _maxValue is string max && value != null &&
        string.CompareOrdinal(value, min) >= 0 && string.CompareOrdinal(value, max) <= 0;
    
    public override bool SatisfyBinary(long time, byte[]? value) => false;
    
    public override Filter Reverse() => throw new NotImplementedException("NotBetween filter");
}

/// <summary>
/// Value is null filter.
/// </summary>
public sealed class ValueIsNullFilter : ValueFilter
{
    public ValueIsNullFilter(int columnIndex = 0) : base(columnIndex) { }
    
    public override OperatorType OperatorType => OperatorType.ValueIsNull;
    
    public override bool Satisfy(long time, object? value) => value == null;
    
    public override bool SatisfyBoolean(long time, bool value) => false;
    
    public override bool SatisfyInteger(long time, int value) => false;
    
    public override bool SatisfyLong(long time, long value) => false;
    
    public override bool SatisfyFloat(long time, float value) => false;
    
    public override bool SatisfyDouble(long time, double value) => false;
    
    public override bool SatisfyString(long time, string? value) => value == null;
    
    public override bool SatisfyBinary(long time, byte[]? value) => value == null;
    
    public override Filter Reverse() => new ValueIsNotNullFilter(ColumnIndex);
}

/// <summary>
/// Value is not null filter.
/// </summary>
public sealed class ValueIsNotNullFilter : ValueFilter
{
    public ValueIsNotNullFilter(int columnIndex = 0) : base(columnIndex) { }
    
    public override OperatorType OperatorType => OperatorType.ValueIsNotNull;
    
    public override bool Satisfy(long time, object? value) => value != null;
    
    public override bool SatisfyBoolean(long time, bool value) => true;
    
    public override bool SatisfyInteger(long time, int value) => true;
    
    public override bool SatisfyLong(long time, long value) => true;
    
    public override bool SatisfyFloat(long time, float value) => true;
    
    public override bool SatisfyDouble(long time, double value) => true;
    
    public override bool SatisfyString(long time, string? value) => value != null;
    
    public override bool SatisfyBinary(long time, byte[]? value) => value != null;
    
    public override Filter Reverse() => new ValueIsNullFilter(ColumnIndex);
}
