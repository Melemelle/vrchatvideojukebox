using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.SDK3.StringLoading;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.Udon.Common.Interfaces;

public class RemoteVideoPlaylist : UdonSharpBehaviour
{
    [Header("Remote Manifest")]
    public VRCUrl manifestUrl;

    [Header("Approved Video URLs")]
    public VRCUrl[] videoUrls;

    [Header("Video Player")]
    public BaseVRCVideoPlayer videoPlayer;

    [Header("Optional UI")]
    public TextMeshProUGUI titleText;
    public GameObject loadingScreen;

    [Header("Startup")]
    public bool playOnStart = true;
    public bool randomStartVideo = false;

    [Header("Safety")]
    public float minimumSwitchDelay = 5f;

    private int[] playlistVideoIndexes;
    private string[] playlistTitles;

    private int currentPlaylistIndex = 0;
    private int currentVideoIndex = 0;

    private float lastUrlLoadTime = -999f;

    private IUdonEventReceiver eventReceiver;

    private void Start()
    {
        Debug.Log("[RemoteVideoPlaylist] START");

        eventReceiver = (IUdonEventReceiver)this;

        if (videoPlayer == null)
        {
            Debug.LogWarning("[RemoteVideoPlaylist] No video player assigned.");
            return;
        }

        if (videoUrls == null || videoUrls.Length == 0)
        {
            Debug.LogWarning("[RemoteVideoPlaylist] No video URLs assigned.");
            return;
        }

        LoadManifest();
    }

    public void LoadManifest()
    {
        if (manifestUrl == null)
        {
            Debug.Log("[RemoteVideoPlaylist] No manifest. Using default playlist.");

            UseDefaultPlaylist();
            ChooseRandomStartIfEnabled();

            if (playOnStart)
            {
                PlayCurrentVideo();
            }

            return;
        }

        Debug.Log("[RemoteVideoPlaylist] Loading manifest: " + manifestUrl);

        VRCStringDownloader.LoadUrl(manifestUrl, eventReceiver);
    }

    public override void OnStringLoadSuccess(IVRCStringDownload result)
    {
        Debug.Log("[RemoteVideoPlaylist] Manifest loaded.");
        Debug.Log(result.Result);

        ParseManifest(result.Result);
        ChooseRandomStartIfEnabled();

        if (playOnStart)
        {
            PlayCurrentVideo();
        }
    }

    public override void OnStringLoadError(IVRCStringDownload result)
    {
        Debug.LogWarning("[RemoteVideoPlaylist] Manifest failed: " + result.Error);

        UseDefaultPlaylist();
        ChooseRandomStartIfEnabled();

        if (playOnStart)
        {
            PlayCurrentVideo();
        }
    }

    private void ParseManifest(string manifest)
    {
        string[] lines = manifest.Split('\n');

        int validCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;

            validCount++;
        }

        if (validCount == 0)
        {
            UseDefaultPlaylist();
            return;
        }

        playlistVideoIndexes = new int[validCount];
        playlistTitles = new string[validCount];

        int playlistSlot = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (line.Length == 0) continue;
            if (line.StartsWith("#")) continue;

            string[] parts = line.Split('|');

            int videoIndex = 0;
            string title = "";

            if (parts.Length > 0)
            {
                int.TryParse(parts[0], out videoIndex);
            }

            if (parts.Length > 1)
            {
                title = parts[1];
            }

            if (videoIndex < 0 || videoIndex >= videoUrls.Length)
            {
                Debug.LogWarning("[RemoteVideoPlaylist] Invalid video index " + videoIndex + ". Replacing with 0.");
                videoIndex = 0;
            }

            playlistVideoIndexes[playlistSlot] = videoIndex;
            playlistTitles[playlistSlot] = title;

            playlistSlot++;
        }
    }

    private void UseDefaultPlaylist()
    {
        playlistVideoIndexes = new int[videoUrls.Length];
        playlistTitles = new string[videoUrls.Length];

        for (int i = 0; i < videoUrls.Length; i++)
        {
            playlistVideoIndexes[i] = i;
            playlistTitles[i] = "Video " + (i + 1);
        }
    }

    private void ChooseRandomStartIfEnabled()
    {
        if (!randomStartVideo) return;
        if (playlistVideoIndexes == null || playlistVideoIndexes.Length == 0) return;

        currentPlaylistIndex = Random.Range(0, playlistVideoIndexes.Length);
    }

    private void PlayCurrentVideo()
    {
        if (playlistVideoIndexes == null || playlistVideoIndexes.Length == 0)
        {
            Debug.LogWarning("[RemoteVideoPlaylist] No playlist available.");
            return;
        }

        currentVideoIndex = playlistVideoIndexes[currentPlaylistIndex];

        if (titleText != null)
        {
            titleText.text = playlistTitles[currentPlaylistIndex];
        }

        float elapsedSinceLastLoad = Time.time - lastUrlLoadTime;

        if (elapsedSinceLastLoad < minimumSwitchDelay)
        {
            float wait = minimumSwitchDelay - elapsedSinceLastLoad;
            SendCustomEventDelayedSeconds("PlayCurrentVideoDelayed", wait);
            return;
        }

        lastUrlLoadTime = Time.time;

        ShowLoadingScreen();

        Debug.Log("[RemoteVideoPlaylist] Playing video index: " + currentVideoIndex);
        Debug.Log("[RemoteVideoPlaylist] URL: " + videoUrls[currentVideoIndex]);

        videoPlayer.PlayURL(videoUrls[currentVideoIndex]);
    }

    public void PlayCurrentVideoDelayed()
    {
        PlayCurrentVideo();
    }

    public override void OnVideoReady()
    {
        Debug.Log("[RemoteVideoPlaylist] Video ready.");

        HideLoadingScreen();

        if (videoPlayer != null)
        {
            videoPlayer.Play();
        }
    }

    public override void OnVideoEnd()
    {
        Debug.Log("[RemoteVideoPlaylist] Video ended. Looping current video.");

        if (videoPlayer != null)
        {
            videoPlayer.PlayURL(videoUrls[currentVideoIndex]);
        }
    }

    public override void OnVideoError(VideoError videoError)
    {
        Debug.LogWarning("[RemoteVideoPlaylist] Video error: " + videoError);

        HideLoadingScreen();
    }

    public void ManualNext()
    {
        GoToNextVideo();
    }

    public void ManualPrevious()
    {
        GoToPreviousVideo();
    }

    private void GoToNextVideo()
    {
        if (playlistVideoIndexes == null || playlistVideoIndexes.Length == 0) return;

        currentPlaylistIndex++;

        if (currentPlaylistIndex >= playlistVideoIndexes.Length)
        {
            currentPlaylistIndex = 0;
        }

        PlayCurrentVideo();
    }

    private void GoToPreviousVideo()
    {
        if (playlistVideoIndexes == null || playlistVideoIndexes.Length == 0) return;

        currentPlaylistIndex--;

        if (currentPlaylistIndex < 0)
        {
            currentPlaylistIndex = playlistVideoIndexes.Length - 1;
        }

        PlayCurrentVideo();
    }

    public void JumpToPlaylistIndex(int playlistIndex)
    {
        if (playlistVideoIndexes == null || playlistVideoIndexes.Length == 0) return;

        if (playlistIndex < 0)
        {
            playlistIndex = 0;
        }

        if (playlistIndex >= playlistVideoIndexes.Length)
        {
            playlistIndex = playlistVideoIndexes.Length - 1;
        }

        currentPlaylistIndex = playlistIndex;
        PlayCurrentVideo();
    }

    public void StopVideo()
    {
        HideLoadingScreen();

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }
    }

    public void PauseVideo()
    {
        if (videoPlayer != null)
        {
            videoPlayer.Pause();
        }
    }

    public void ResumeVideo()
    {
        HideLoadingScreen();

        if (videoPlayer != null)
        {
            videoPlayer.Play();
        }
    }

    private void ShowLoadingScreen()
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(true);
        }
    }

    private void HideLoadingScreen()
    {
        if (loadingScreen != null)
        {
            loadingScreen.SetActive(false);
        }
    }
}
