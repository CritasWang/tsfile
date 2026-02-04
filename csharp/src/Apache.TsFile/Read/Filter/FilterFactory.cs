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
/// Factory class for creating and combining filters.
/// </summary>
public static class FilterFactory
{
    /// <summary>
    /// Combines two filters with AND logic.
    /// </summary>
    /// <param name="left">The left filter.</param>
    /// <param name="right">The right filter.</param>
    /// <returns>A new filter representing the AND of the two filters, or the non-null filter if one is null.</returns>
    public static Filter? And(Filter? left, Filter? right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return new AndFilter(left, right);
    }
    
    /// <summary>
    /// Combines two filters with OR logic.
    /// </summary>
    /// <param name="left">The left filter.</param>
    /// <param name="right">The right filter.</param>
    /// <returns>A new filter representing the OR of the two filters, or the non-null filter if one is null.</returns>
    public static Filter? Or(Filter? left, Filter? right)
    {
        if (left == null) return right;
        if (right == null) return left;
        return new OrFilter(left, right);
    }
    
    /// <summary>
    /// Creates a NOT filter.
    /// </summary>
    /// <param name="filter">The filter to negate.</param>
    /// <returns>A new filter representing the negation of the input filter.</returns>
    public static Filter? Not(Filter? filter)
    {
        if (filter == null) return null;
        return new NotFilter(filter);
    }
}

/// <summary>
/// API for creating time-based filters.
/// </summary>
public static class TimeFilterApi
{
    /// <summary>
    /// Creates a filter for timestamps equal to the specified value.
    /// </summary>
    public static TimeFilter Eq(long value) => TimeFilter.Eq(value);
    
    /// <summary>
    /// Creates a filter for timestamps not equal to the specified value.
    /// </summary>
    public static TimeFilter NotEq(long value) => TimeFilter.NotEq(value);
    
    /// <summary>
    /// Creates a filter for timestamps greater than the specified value.
    /// </summary>
    public static TimeFilter Gt(long value) => TimeFilter.Gt(value);
    
    /// <summary>
    /// Creates a filter for timestamps greater than or equal to the specified value.
    /// </summary>
    public static TimeFilter GtEq(long value) => TimeFilter.GtEq(value);
    
    /// <summary>
    /// Creates a filter for timestamps less than the specified value.
    /// </summary>
    public static TimeFilter Lt(long value) => TimeFilter.Lt(value);
    
    /// <summary>
    /// Creates a filter for timestamps less than or equal to the specified value.
    /// </summary>
    public static TimeFilter LtEq(long value) => TimeFilter.LtEq(value);
    
    /// <summary>
    /// Creates a filter for timestamps between the specified values (inclusive).
    /// </summary>
    public static TimeFilter Between(long min, long max) => TimeFilter.Between(min, max);
    
    /// <summary>
    /// Creates a filter for timestamps not between the specified values.
    /// </summary>
    public static TimeFilter NotBetween(long min, long max) => TimeFilter.NotBetween(min, max);
}

/// <summary>
/// API for creating value-based filters.
/// </summary>
public static class ValueFilterApi
{
    /// <summary>
    /// Creates a filter for values equal to the specified value.
    /// </summary>
    public static ValueFilter Eq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => ValueFilter.Eq(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values not equal to the specified value.
    /// </summary>
    public static ValueFilter NotEq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => ValueFilter.NotEq(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values greater than the specified value.
    /// </summary>
    public static ValueFilter Gt<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => ValueFilter.Gt(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values greater than or equal to the specified value.
    /// </summary>
    public static ValueFilter GtEq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => ValueFilter.GtEq(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values less than the specified value.
    /// </summary>
    public static ValueFilter Lt<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => ValueFilter.Lt(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values less than or equal to the specified value.
    /// </summary>
    public static ValueFilter LtEq<T>(T value, int columnIndex = 0) where T : IComparable<T>
        => ValueFilter.LtEq(value, columnIndex);
    
    /// <summary>
    /// Creates a filter for values between the specified values (inclusive).
    /// </summary>
    public static ValueFilter Between<T>(T min, T max, int columnIndex = 0) where T : IComparable<T>
        => ValueFilter.Between(min, max, columnIndex);
    
    /// <summary>
    /// Creates a filter for null values.
    /// </summary>
    public static ValueFilter IsNull(int columnIndex = 0) => ValueFilter.IsNull(columnIndex);
    
    /// <summary>
    /// Creates a filter for non-null values.
    /// </summary>
    public static ValueFilter IsNotNull(int columnIndex = 0) => ValueFilter.IsNotNull(columnIndex);
}
