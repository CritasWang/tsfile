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

package org.apache.tsfile.read.common.block.column;

import java.util.UUID;
import java.util.concurrent.atomic.AtomicLong;

public class DictionaryId {
  private static final UUID nodeId = UUID.randomUUID();
  private static final AtomicLong sequenceGenerator = new AtomicLong();

  private final long mostSignificantBits;
  private final long leastSignificantBits;
  private final long sequenceId;

  public static DictionaryId randomDictionaryId() {
    return new DictionaryId(
        nodeId.getMostSignificantBits(),
        nodeId.getLeastSignificantBits(),
        sequenceGenerator.getAndIncrement());
  }

  public DictionaryId(long mostSignificantBits, long leastSignificantBits, long sequenceId) {
    this.mostSignificantBits = mostSignificantBits;
    this.leastSignificantBits = leastSignificantBits;
    this.sequenceId = sequenceId;
  }

  public long getMostSignificantBits() {
    return mostSignificantBits;
  }

  public long getLeastSignificantBits() {
    return leastSignificantBits;
  }

  public long getSequenceId() {
    return sequenceId;
  }

  @Override
  public boolean equals(Object o) {
    if (this == o) {
      return true;
    }
    if (o == null || getClass() != o.getClass()) {
      return false;
    }
    DictionaryId that = (DictionaryId) o;
    return mostSignificantBits == that.mostSignificantBits
        && leastSignificantBits == that.leastSignificantBits
        && sequenceId == that.sequenceId;
  }

  @Override
  public int hashCode() {
    int hashCode = 31 + Long.hashCode(mostSignificantBits);
    hashCode = (hashCode * 31) + Long.hashCode(leastSignificantBits);
    hashCode = (hashCode * 31) + Long.hashCode(sequenceId);
    return hashCode;
  }
}
