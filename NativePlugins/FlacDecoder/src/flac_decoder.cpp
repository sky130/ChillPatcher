#define DR_FLAC_IMPLEMENTATION
#define BUILDING_DLL  // 定义为导出模式
#include "dr_flac.h"
#include "flac_decoder.h"

#include <string>
#include <cstring>
#include <cstdlib>

// 线程本地错误消息
static thread_local std::string g_last_error;

extern "C" {

FLAC_API int DecodeFlacFile(const wchar_t* file_path, FlacAudioInfo* out_info) {
    if (!file_path || !out_info) {
        g_last_error = "Invalid parameters";
        return -1;
    }

    // 清零输出结构
    memset(out_info, 0, sizeof(FlacAudioInfo));

    // ✅ 修改点：使用 drflac_open_file_w 支持宽字符路径
    drflac* flac = drflac_open_file_w(file_path, nullptr);
    
    if (!flac) {
        // 注意：file_path 是宽字符，不能直接加到 std::string (std::string 是 char)
        // 为了简单起见，这里只记录通用错误，避免字符串转换导致的崩溃
        g_last_error = "Failed to open FLAC file (Path resolution failed)";
        return -2;
    }

    // 获取音频信息
    out_info->sample_rate = flac->sampleRate;
    out_info->channels = flac->channels;
    out_info->total_pcm_frame_count = flac->totalPCMFrameCount;

    // 计算 PCM 数据大小
    size_t total_samples = (size_t)flac->totalPCMFrameCount * flac->channels;
    out_info->pcm_data_size = total_samples * sizeof(float);

    // 分配内存
    out_info->pcm_data = static_cast<float*>(malloc(out_info->pcm_data_size));
    if (!out_info->pcm_data) {
        g_last_error = "Failed to allocate memory for PCM data";
        drflac_close(flac);
        return -3;
    }

    // 解码为 float PCM
    drflac_uint64 frames_read = drflac_read_pcm_frames_f32(
        flac, 
        flac->totalPCMFrameCount, 
        out_info->pcm_data
    );

    drflac_close(flac);

    if (frames_read != flac->totalPCMFrameCount) {
        g_last_error = "Failed to read all PCM frames";
        free(out_info->pcm_data);
        out_info->pcm_data = nullptr;
        return -4;
    }

    return 0; // 成功
}

FLAC_API void FreeFlacData(FlacAudioInfo* info) {
    if (info && info->pcm_data) {
        free(info->pcm_data);
        info->pcm_data = nullptr;
        info->pcm_data_size = 0;
    }
}

FLAC_API const char* FlacGetLastError() {
    return g_last_error.c_str();
}

// ========== 流式解码实现 ==========

FLAC_API void* OpenFlacStream(const wchar_t* file_path, int* out_sample_rate, int* out_channels, unsigned long long* out_total_pcm_frames) {
    if (!file_path) {
        g_last_error = "File path is NULL";
        return nullptr;
    }

    // ✅ 修改点：使用 drflac_open_file_w 支持宽字符路径
    drflac* flac = drflac_open_file_w(file_path, nullptr);
    
    if (!flac) {
        g_last_error = "Failed to open FLAC file for streaming";
        return nullptr;
    }

    // 输出音频信息
    if (out_sample_rate) *out_sample_rate = flac->sampleRate;
    if (out_channels) *out_channels = flac->channels;
    if (out_total_pcm_frames) *out_total_pcm_frames = flac->totalPCMFrameCount;

    return static_cast<void*>(flac);
}

FLAC_API long long ReadFlacFrames(void* stream_handle, float* buffer, unsigned long long frames_to_read) {
    if (!stream_handle) {
        g_last_error = "Stream handle is NULL";
        return -1;
    }
    if (!buffer) {
        g_last_error = "Buffer is NULL";
        return -1;
    }

    drflac* flac = static_cast<drflac*>(stream_handle);
    
    // dr_flac 返回实际读取的帧数
    drflac_uint64 frames_read = drflac_read_pcm_frames_f32(flac, frames_to_read, buffer);
    
    return static_cast<long long>(frames_read);
}

FLAC_API int SeekFlacStream(void* stream_handle, unsigned long long frame_index) {
    if (!stream_handle) {
        g_last_error = "Stream handle is NULL";
        return -1;
    }

    drflac* flac = static_cast<drflac*>(stream_handle);
    
    // drflac_seek_to_pcm_frame 返回 DRFLAC_TRUE/DRFLAC_FALSE
    drflac_bool32 success = drflac_seek_to_pcm_frame(flac, frame_index);
    
    if (!success) {
        g_last_error = "Failed to seek to specified frame";
        return -1;
    }

    return 0;
}

FLAC_API void CloseFlacStream(void* stream_handle) {
    if (stream_handle) {
        drflac* flac = static_cast<drflac*>(stream_handle);
        drflac_close(flac);
    }
}

} // extern "C"
