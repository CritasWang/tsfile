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
using Apache.TsFile.Read.Common;
using Apache.TsFile.Read.Filter;

namespace Apache.TsFile.Read.Reader;

/// <summary>
/// Interface for reading page data from a TsFile.
/// </summary>
public interface IPageReader
{
    /// <summary>
    /// Gets all data from the page that satisfies the filter.
    /// </summary>
    /// <param name="ascending">Whether to return data in ascending order.</param>
    /// <returns>BatchData containing the satisfied data.</returns>
    BatchData GetAllSatisfiedPageData(bool ascending = true);
    
    /// <summary>
    /// Gets all satisfied data as a TsBlock (columnar format).
    /// </summary>
    /// <returns>TsBlock containing the satisfied data.</returns>
    TsBlock GetAllSatisfiedData();
    
    /// <summary>
    /// Adds a record filter to this page reader.
    /// </summary>
    /// <param name="filter">The filter to add.</param>
    void AddRecordFilter(Filter.Filter filter);
    
    /// <summary>
    /// Gets whether the page has been modified (has deletions).
    /// </summary>
    bool IsModified { get; }
    
    /// <summary>
    /// Sets whether the page has been modified.
    /// </summary>
    void SetModified(bool modified);
    
    /// <summary>
    /// Initializes the TsBlock builder with the specified data types.
    /// </summary>
    /// <param name="dataTypes">The data types for the columns.</param>
    void InitTsBlockBuilder(IReadOnlyList<TsDataType> dataTypes);
    
    /// <summary>
    /// Sets the pagination controller for limit/offset queries.
    /// </summary>
    /// <param name="paginationController">The pagination controller.</param>
    void SetLimitOffset(PaginationController paginationController);
    
    /// <summary>
    /// Gets statistics about this page.
    /// </summary>
    IStatistics? Statistics { get; }
}

/// <summary>
/// Controller for pagination (limit and offset) in queries.
/// </summary>
public class PaginationController
{
    /// <summary>
    /// The default unlimited pagination controller.
    /// </summary>
    public static readonly PaginationController Unlimited = new(long.MaxValue, 0);
    
    private long _curLimit;
    private long _curOffset;
    
    /// <summary>
    /// Creates a new pagination controller.
    /// </summary>
    /// <param name="limit">The maximum number of rows to return.</param>
    /// <param name="offset">The number of rows to skip.</param>
    public PaginationController(long limit, long offset)
    {
        _curLimit = limit;
        _curOffset = offset;
    }
    
    /// <summary>
    /// Gets whether a limit has been set.
    /// </summary>
    public bool HasSetLimit => _curLimit < long.MaxValue;
    
    /// <summary>
    /// Gets the current limit.
    /// </summary>
    public long CurLimit => _curLimit;
    
    /// <summary>
    /// Gets whether there is remaining offset to skip.
    /// </summary>
    public bool HasCurOffset => _curOffset > 0;
    
    /// <summary>
    /// Gets whether there is remaining limit to return.
    /// </summary>
    public bool HasCurLimit => _curLimit > 0;
    
    /// <summary>
    /// Consumes one from the offset count.
    /// </summary>
    public void ConsumeOffset()
    {
        if (_curOffset > 0)
            _curOffset--;
    }
    
    /// <summary>
    /// Consumes one from the limit count.
    /// </summary>
    public void ConsumeLimit()
    {
        if (_curLimit > 0)
            _curLimit--;
    }
}
