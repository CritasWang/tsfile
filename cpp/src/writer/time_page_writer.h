/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * License); you may not use this file except in compliance
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
#ifndef WRITER_PAGE_TIME_WRITER_H
#define WRITER_PAGE_TIME_WRITER_H

#include "common/allocator/byte_stream.h"
#include "common/statistic.h"
#include "compress/compressor.h"
#include "encoding/encoder.h"
#include "utils/db_utils.h"

namespace storage {

struct TimePageData {
    uint32_t time_buf_size_;
    uint32_t uncompressed_size_;
    uint32_t compressed_size_;
    char *uncompressed_buf_;
    char *compressed_buf_;
    Compressor *compressor_;

    TimePageData()
        : time_buf_size_(0),
          uncompressed_size_(0),
          compressed_size_(0),
          uncompressed_buf_(nullptr),
          compressed_buf_(nullptr),
          compressor_(nullptr) {}
    int init(common::ByteStream &time_bs, Compressor *compressor);
    void destroy() {
        if (uncompressed_buf_ != nullptr) {
            common::mem_free(uncompressed_buf_);
            uncompressed_buf_ = nullptr;
        }
        if (compressed_buf_ != nullptr && compressor_ != nullptr) {
            compressor_->after_compress(compressed_buf_);
            compressed_buf_ = nullptr;
        }
    }
};

class TimePageWriter {
   public:
    TimePageWriter()
        : data_type_(common::VECTOR),
          time_encoder_(nullptr),
          statistic_(nullptr),
          time_out_stream_(OUT_STREAM_PAGE_SIZE,
                           common::MOD_PAGE_WRITER_OUTPUT_STREAM),
          cur_page_data_(),
          compressor_(nullptr),
          is_inited_(false) {}
    ~TimePageWriter() { destroy(); }
    int init(common::TSEncoding encoding, common::CompressionType compression);
    void reset();
    void destroy();

    FORCE_INLINE int write(int64_t timestamp) {
        int ret = common::E_OK;
        if (RET_FAIL(time_encoder_->encode(timestamp, time_out_stream_))) {
        } else {
            statistic_->update(timestamp);
        }
        return ret;
    }

    FORCE_INLINE uint32_t get_point_numer() const { return statistic_->count_; }
    FORCE_INLINE uint32_t get_time_out_stream_size() const {
        return time_out_stream_.total_size();
    }
    FORCE_INLINE uint32_t get_page_memory_size() const {
        return time_out_stream_.total_size();
    }
    FORCE_INLINE uint32_t estimate_max_mem_size() const {
        return time_out_stream_.total_size() +
               time_encoder_->get_max_byte_size();
    }
    int write_to_chunk(common::ByteStream &pages_data, bool write_header,
                       bool write_statistic, bool write_data_to_chunk_data);
    FORCE_INLINE common::ByteStream &get_time_data() {
        return time_out_stream_;
    }
    FORCE_INLINE Statistic *get_statistic() { return statistic_; }
    TimePageData get_cur_page_data() { return cur_page_data_; }
    void destroy_page_data() { cur_page_data_.destroy(); }

   private:
    FORCE_INLINE int prepare_end_page() {
        int ret = common::E_OK;
        if (RET_FAIL(time_encoder_->flush(time_out_stream_))) {
        }
        return ret;
    }
    int copy_page_data_to(common::ByteStream &my_page_data,
                          common::ByteStream &pages_data);

   private:
    static const uint32_t OUT_STREAM_PAGE_SIZE = 1024;

   private:
    common::TSDataType data_type_;
    Encoder *time_encoder_;
    Statistic *statistic_;
    common::ByteStream time_out_stream_;
    TimePageData cur_page_data_;
    Compressor *compressor_;
    bool is_inited_;
};

}  // end namespace storage

#endif  // WRITER_PAGE_TIME_WRITER_H