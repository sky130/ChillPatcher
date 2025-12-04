using System;

namespace ChillPatcher.UIFramework.Music.ThirdPlayer
{
    // public sealed class MusicModule : NancyModule
    // {
    //     public MusicModule()
    //     {
            // Get["/updateMusic"] = _ =>
            // {
            //     try
            //     {
            //         var music = this.Bind<MusicInfo>();
            //         MusicUIController.UpdateMusic(music.title, music.artist);
            //         return Response.AsJson(new { status = "updated" });
            //     }
            //     catch (Exception ex)
            //     {
            //         return Response.AsJson(new { error = ex.Message }, HttpStatusCode.BadRequest);
            //     }
            // };
            //
            // Get["/updatePlayback"] = _ =>
            // {
            //     try
            //     {
            //         var playback = this.Bind<Playback>();
            //         MusicUIController.UpdatePlayback(playback.progress, playback.isPlaying);
            //         return Response.AsJson(new { status = "updated" });
            //     }
            //     catch (Exception ex)
            //     {
            //         return Response.AsJson(new { error = ex.Message }, HttpStatusCode.BadRequest);
            //     }
            // };
            //
            // Get["/updateCover"] = _ =>
            // {
            //     try
            //     {
            //         var cover = this.Bind<CoverUploadRequest>();
            //         var coverImageData = Convert.FromBase64String(cover.imageData);
            //         var imageContentType = cover.contentType ?? "image/jpeg";
            //         
            //         // 这里添加你的封面处理逻辑
            //         // 例如：MusicUIController.UpdateCover(coverImageData, imageContentType);
            //         
            //         return Response.AsJson(new { status = "uploaded" });
            //     }
            //     catch (FormatException)
            //     {
            //         return Response.AsJson(new { error = "Invalid base64 data" }, HttpStatusCode.BadRequest);
            //     }
            //     catch (Exception ex)
            //     {
            //         return Response.AsJson(new { error = ex.Message }, HttpStatusCode.InternalServerError);
            //     }
            // };
        // }
    // }
}
