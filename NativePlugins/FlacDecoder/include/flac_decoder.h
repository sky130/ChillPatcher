#ifndef CHILL_FLAC_DECODER_H
#define CHILL_FLAC_DECODER_H

#ifdef __cplusplus
extern "C" {
#endif

// 导出符号宏
#ifdef _WIN32
    #ifdef BUILDING_DLL
        #define FLAC_API __declspec(dllexport)
    #else
        #define FLAC_API __declspec(dllimport)
    #endif
#else
    #define FLAC_API
#endif

// FLAC 音频信息结构
typedef struct {
    int sample_rate;       // 采样率（如 44100, 48000）
    int channels;          // 声道数（1=单声道, 2=立体声）
    unsigned long long total_pcm_frame_count;  // PCM 帧总数
    float* pcm_data;       // PCM 数据（交错格式，范围 [-1.0, 1.0]）
    size_t pcm_data_size;  // PCM 数据字节数
} FlacAudioInfo;

/**
 * 解码 FLAC 文件为 PCM 数据
 * 
 * @param file_path FLAC 文件路径（UTF-8 编码）
 * @param out_info 输出音频信息（调用者需要调用 FreeFlacData 释放）
 * @return 0=成功, 非0=错误码
 */
FLAC_API int DecodeFlacFile(const wchar_t* file_path, FlacAudioInfo* out_info);

/**
 * 释放解码后的 PCM 数据
 * 
 * @param info 要释放的音频信息
 */
FLAC_API void FreeFlacData(FlacAudioInfo* info);

/**
 * 获取最后的错误消息
 * 
 * @return 错误消息字符串（UTF-8 编码）
 */
FLAC_API const char* FlacGetLastError();

// ========== 流式解码 API ==========

/**
 * 打开 FLAC 文件用于流式读取
 * 
 * @param file_path FLAC 文件路径（UTF-8 编码）
 * @param out_sample_rate 输出采样率
 * @param out_channels 输出声道数
 * @param out_total_pcm_frames 输出总帧数
 * @return 流句柄，失败返回 NULL
 */
FLAC_API void* OpenFlacStream(const wchar_t* file_path, int* out_sample_rate, int* out_channels, unsigned long long* out_total_pcm_frames);

/**
 * 从 FLAC 流读取 PCM 帧
 * 
 * @param stream_handle 流句柄（由 OpenFlacStream 返回）
 * @param buffer 输出缓冲区（float 数组，交错格式）
 * @param frames_to_read 要读取的帧数
 * @return 实际读取的帧数，0表示到达末尾，-1表示错误
 */
FLAC_API long long ReadFlacFrames(void* stream_handle, float* buffer, unsigned long long frames_to_read);

/**
 * 定位到指定的 PCM 帧位置
 * 
 * @param stream_handle 流句柄
 * @param frame_index 目标帧索引
 * @return 0=成功, 非0=失败
 */
FLAC_API int SeekFlacStream(void* stream_handle, unsigned long long frame_index);

/**
 * 关闭 FLAC 流
 * 
 * @param stream_handle 流句柄
 */
FLAC_API void CloseFlacStream(void* stream_handle);

#ifdef __cplusplus
}
#endif

#endif // CHILL_FLAC_DECODER_H
