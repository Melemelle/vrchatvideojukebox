using UdonSharp;
using UnityEngine;

public class VideoPlaylistUIButton : UdonSharpBehaviour
{
    [Header("Playlist")]
    public RemoteVideoPlaylist playlist;

    [Header("Button Type")]
    public bool isNextButton = true;

    public void PressButton()
    {
        if (playlist == null) return;

        if (isNextButton)
        {
            playlist.ManualNext();
        }
        else
        {
            playlist.ManualPrevious();
        }
    }
}
