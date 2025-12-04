using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ChillPatcher.Patches.UIFramework;
using HarmonyLib;
using Refit;

namespace ChillPatcher.UIFramework.Music.ThirdPlayer
{
    public class ThirdPlayerController
    {
        private static ThirdPlayerController _instance;
        private static IMusicApi _musicApi;
        public static ThirdPlayerController Instance => _instance;
        public static IMusicApi RemoteControllerApi => _musicApi;

        private HttpServer server;


        public static void Init()
        {
            _musicApi = RestService.For<IMusicApi>("http://localhost:8092");
            _instance = new ThirdPlayerController();
            _instance.Initialize();
        }

        public ThirdPlayerController()
        {
            Plugin.Logger.LogError("[ThirdPlayer] ThirdPlayerController is created.");
        }

        public async void Initialize()
        {
            await Task.Run(() =>
            {
                try
                {
                    var port = UIFrameworkConfig.ThirdPlayerMediaTransportControlsPort.Value;
                    Plugin.Logger.LogInfo($"[ThirdPlayer] Server port is {port}.");
                    server = new HttpServer(port);
                    SetupRoutes();
                    Plugin.Logger.LogInfo($"[ThirdPlayer] Server try to setup routes.");
                    Plugin.Logger.LogInfo($"[ThirdPlayer] Server try to start.");
                    server.Start();
                    Plugin.Logger.LogInfo("[ThirdPlayer] Server is running.");
                }
                catch (Exception e)
                {
                    Plugin.Logger.LogError("[ThirdPlayer] " + e.Message);
                }
            });
        }

        void Get(string path, Func<Request, Response> handler) => server.Get(path, handler);
        void Post(string path, Func<Request, Response> handler) => server.Post(path, handler);


        public void SetupRoutes()
        {
            Get("/test", req => Response.Json(new
                    {
                        message = "Hello " + req.Query.GetOrDefault("message", "World")
                    }
                )
            );
            
            // http://127.0.0.1:8091/setMusicInfo?title=Hello%20World&artist=Suki
            Get("/setMusicInfo", req =>
            {
                var title = req.Query.GetOrDefault("title", "Null");
                var artist = req.Query.GetOrDefault("artist", "Null");
                MusicUIController.UpdateMusic(title, artist);
                return Response.Ok();
            });
            
            // http://127.0.0.1:8091/setPlayback?progress=10&isPlaying=true
            Get("/setPlayback", req =>
            {
                
                var progress = int.TryParse(req.Query.GetOrDefault("progress", "0"), out var value) ? value : 0;
                var isPlaying = req.Query.GetOrDefault("isPlaying", "false") == "true";
                Plugin.Logger.LogInfo($"[ThirdPlayer] Setting progress is {progress}.");
                MusicUIController.UpdatePlayback(progress, isPlaying);
                return Response.Ok();
            });
        }
    }
}