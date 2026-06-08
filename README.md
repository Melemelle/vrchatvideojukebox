# vrchatvideojukebox
Video jukebox player for VRchat

Below is the v2 upgraded **remote video library player** version.

It supports:

```text
Previous / Next
Specific video buttons / thumbnails
Play / Pause / Stop
Optional title text
Optional loading screen
Remote manifest
Loop current video until another button is pressed
```

---

# 1. Main script: `RemoteVideoPlaylist.cs`

```csharp
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
```

---

# 2. Next / Previous button script: `VideoPlaylistUIButton.cs`

```csharp
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
```

---

# 3. Direct video / thumbnail button script: `VideoPlaylistJumpButton.cs`

Use this for buttons that jump to a specific video.

```csharp
using UdonSharp;
using UnityEngine;

public class VideoPlaylistJumpButton : UdonSharpBehaviour
{
    [Header("Playlist")]
    public RemoteVideoPlaylist playlist;

    [Header("Target")]
    public int playlistIndex = 0;

    public void PressButton()
    {
        if (playlist == null) return;

        playlist.JumpToPlaylistIndex(playlistIndex);
    }
}
```

---

# 4. Play / Pause / Stop button script: `VideoPlaybackUIButton.cs`

```csharp
using UdonSharp;
using UnityEngine;

public class VideoPlaybackUIButton : UdonSharpBehaviour
{
    [Header("Playlist")]
    public RemoteVideoPlaylist playlist;

    [Header("Control Type")]
    public int controlType = 0;

    // 0 = Play / Resume
    // 1 = Pause
    // 2 = Stop

    public void PressButton()
    {
        if (playlist == null) return;

        if (controlType == 0)
        {
            playlist.ResumeVideo();
        }
        else if (controlType == 1)
        {
            playlist.PauseVideo();
        }
        else if (controlType == 2)
        {
            playlist.StopVideo();
        }
    }
}
```

---

# 5. Manifest example

Create:

```text
video_manifest.txt
```

Example:

```text
0|Forest Loop
1|Ocean Loop
2|Space Tunnel
3|Abstract Pattern
```

The numbers refer to your Unity `Video URLs` array.

---

# 6. Unity hierarchy

Create something like:

```text
RemoteVideoLibrary
├── VideoPlayer
├── VideoScreen
├── LoadingScreen
├── Canvas
│   ├── PrevButton
│   ├── NextButton
│   ├── PlayButton
│   ├── PauseButton
│   ├── StopButton
│   ├── Video01ThumbnailButton
│   ├── Video02ThumbnailButton
│   └── TitleText
└── RemoteVideoPlaylist
```

---

# 7. Video player setup

Add a VRChat video player component/prefab.

Use:

```text
VRCUnityVideoPlayer
```

for your current setup.

Assign it to:

```text
RemoteVideoPlaylist → Video Player
```

---

# 8. RemoteVideoPlaylist inspector setup

On the object with `RemoteVideoPlaylist.cs`:

```text
Manifest URL:
https://yourname.github.io/vr-gallery/video_manifest.txt

Video URLs:
Element 0 = direct video URL
Element 1 = direct video URL
Element 2 = direct video URL

Video Player:
drag your VRCUnityVideoPlayer here

Title Text:
drag TitleText here

Loading Screen:
drag LoadingScreen object here

Play On Start:
enabled

Random Start Video:
optional
```

---

# 9. Loading screen setup

Create a plane or UI panel called:

```text
LoadingScreen
```

Put it slightly in front of the video screen.

Use a material/image with:

```text
black background
loading icon
LOADING...
```

Set it inactive by default:

```text
LoadingScreen active = false
```

The script will turn it on while loading and hide it when the video is ready.

---

# 10. Previous / Next buttons

On `PrevButton`, add:

```text
VideoPlaylistUIButton
Playlist = RemoteVideoPlaylist
Is Next Button = false
```

On `NextButton`, add:

```text
VideoPlaylistUIButton
Playlist = RemoteVideoPlaylist
Is Next Button = true
```

In each Unity Button component:

```text
OnClick()
→ UdonBehaviour
→ SendCustomEvent(string)
→ PressButton
```

---

# 11. Play / Pause / Stop buttons

On `PlayButton`, add:

```text
VideoPlaybackUIButton
Playlist = RemoteVideoPlaylist
Control Type = 0
```

On `PauseButton`:

```text
Control Type = 1
```

On `StopButton`:

```text
Control Type = 2
```

Each Button `OnClick()`:

```text
UdonBehaviour
→ SendCustomEvent(string)
→ PressButton
```

---

# 12. Specific video thumbnail buttons

For `Video01ThumbnailButton`, add:

```text
VideoPlaylistJumpButton
Playlist = RemoteVideoPlaylist
Playlist Index = 0
```

For `Video02ThumbnailButton`:

```text
Playlist Index = 1
```

For `Video03ThumbnailButton`:

```text
Playlist Index = 2
```

Again, each Button `OnClick()`:

```text
UdonBehaviour
→ SendCustomEvent(string)
→ PressButton
```

---

# 13. Thumbnail image setup

On each thumbnail button:

1. Select the button.
2. Find the `Image` component.
3. Set your thumbnail PNG as `Source Image`.

Make sure thumbnail textures are imported as:

```text
Texture Type: Sprite (2D and UI)
```

---
set the unity video player to loop if needed!!

This creates a remote video library: clickable thumbnails, player controls, looping videos, a loading screen, and remote playlist titles.

