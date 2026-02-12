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

namespace Apache.TsFile.IO;

/// <summary>
/// Base interface for value filters used in TsFile queries.
/// </summary>
public interface IValueFilter
{
    /// <summary>
    /// Tests whether a row passes this filter.
    /// </summary>
    /// <param name="timestamp">Row timestamp.</param>
    /// <param name="getMeasurementValue">Function to get a measurement value by name for this row.</param>
    bool Matches(long timestamp, Func<string, object?> getMeasurementValue);
}

/// <summary>
/// Compares a measurement value against a threshold.
/// </summary>
public class ComparisonFilter : IValueFilter
{
    public string MeasurementName { get; }
    public ComparisonOp Op { get; }
    public IComparable Threshold { get; }

    public ComparisonFilter(string measurementName, ComparisonOp op, IComparable threshold)
    {
        MeasurementName = measurementName;
        Op = op;
        Threshold = threshold;
    }

    public bool Matches(long timestamp, Func<string, object?> getMeasurementValue)
    {
        var value = getMeasurementValue(MeasurementName);
        if (value == null) return false;

        int cmp;
        try
        {
            cmp = ((IComparable)value).CompareTo(Convert.ChangeType(Threshold, value.GetType()));
        }
        catch
        {
            return false;
        }

        return Op switch
        {
            ComparisonOp.Eq => cmp == 0,
            ComparisonOp.Ne => cmp != 0,
            ComparisonOp.Lt => cmp < 0,
            ComparisonOp.Le => cmp <= 0,
            ComparisonOp.Gt => cmp > 0,
            ComparisonOp.Ge => cmp >= 0,
            _ => false
        };
    }
}

/// <summary>
/// Logical AND of multiple filters.
/// </summary>
public class AndFilter : IValueFilter
{
    public IReadOnlyList<IValueFilter> Filters { get; }

    public AndFilter(params IValueFilter[] filters)
    {
        Filters = filters;
    }

    public bool Matches(long timestamp, Func<string, object?> getMeasurementValue)
    {
        foreach (var f in Filters)
            if (!f.Matches(timestamp, getMeasurementValue))
                return false;
        return true;
    }
}

/// <summary>
/// Logical OR of multiple filters.
/// </summary>
public class OrFilter : IValueFilter
{
    public IReadOnlyList<IValueFilter> Filters { get; }

    public OrFilter(params IValueFilter[] filters)
    {
        Filters = filters;
    }

    public bool Matches(long timestamp, Func<string, object?> getMeasurementValue)
    {
        foreach (var f in Filters)
            if (f.Matches(timestamp, getMeasurementValue))
                return true;
        return false;
    }
}

/// <summary>
/// Logical NOT of a filter.
/// </summary>
public class NotFilter : IValueFilter
{
    public IValueFilter Inner { get; }

    public NotFilter(IValueFilter inner)
    {
        Inner = inner;
    }

    public bool Matches(long timestamp, Func<string, object?> getMeasurementValue)
    {
        return !Inner.Matches(timestamp, getMeasurementValue);
    }
}

/// <summary>
/// Comparison operators for value filters.
/// </summary>
public enum ComparisonOp
{
    Eq, Ne, Lt, Le, Gt, Ge
}

/// <summary>
/// Fluent builder for constructing value filters.
/// </summary>
public static class Filter
{
    public static ComparisonFilter Eq(string measurement, IComparable value) =>
        new(measurement, ComparisonOp.Eq, value);

    public static ComparisonFilter Ne(string measurement, IComparable value) =>
        new(measurement, ComparisonOp.Ne, value);

    public static ComparisonFilter Lt(string measurement, IComparable value) =>
        new(measurement, ComparisonOp.Lt, value);

    public static ComparisonFilter Le(string measurement, IComparable value) =>
        new(measurement, ComparisonOp.Le, value);

    public static ComparisonFilter Gt(string measurement, IComparable value) =>
        new(measurement, ComparisonOp.Gt, value);

    public static ComparisonFilter Ge(string measurement, IComparable value) =>
        new(measurement, ComparisonOp.Ge, value);

    public static AndFilter And(params IValueFilter[] filters) => new(filters);
    public static OrFilter Or(params IValueFilter[] filters) => new(filters);
    public static NotFilter Not(IValueFilter filter) => new(filter);
}
