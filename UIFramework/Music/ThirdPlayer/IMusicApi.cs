using System.Threading.Tasks;
using Refit;

namespace ChillPatcher.UIFramework.Music.ThirdPlayer;

public interface IMusicApi
{
    [Get("/play")]
    Task Play();

    [Get("/pause")]
    Task Pause();
    
    [Get("/next")]
    Task Next();
    
    [Get("/prev")]
    Task Prev();
}