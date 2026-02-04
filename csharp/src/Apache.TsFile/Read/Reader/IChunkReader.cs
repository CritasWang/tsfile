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

using Apache.TsFile.Read.Common;

namespace Apache.TsFile.Read.Reader;

/// <summary>
/// Interface for reading chunk data from a TsFile.
/// A chunk contains multiple pages of data for a single timeseries.
/// </summary>
public interface IChunkReader : IDisposable
{
    /// <summary>
    /// Gets whether there is a next page that satisfies the filter.
    /// </summary>
    /// <returns>True if there is more data to read.</returns>
    bool HasNextSatisfiedPage();
    
    /// <summary>
    /// Gets the data from the next satisfied page.
    /// </summary>
    /// <returns>BatchData containing the page data.</returns>
    BatchData NextPageData();
    
    /// <summary>
    /// Loads and returns all page readers for this chunk.
    /// </summary>
    /// <returns>A list of page readers.</returns>
    IReadOnlyList<IPageReader> LoadPageReaderList();
}
