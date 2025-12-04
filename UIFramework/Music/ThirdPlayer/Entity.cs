namespace ChillPatcher.UIFramework.Music.ThirdPlayer;

public class MusicInfo
{
    public string title { get; set; }
    public string artist { get; set; }
}

public class Playback
{
    public int progress { get; set; } // 0 ~ 100 
    public bool isPlaying { get; set; }
}

public class CoverUploadRequest
{
    public string imageData { get; set; } // Base64 编码的图片数据
    public string contentType { get; set; } // 图片类型，如 "image/jpeg"
}


public class Callback
{
    public string isPlaying { get; set; }
}
