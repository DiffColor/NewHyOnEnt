using System;
using System.Collections.Generic;

namespace NewHyOnPlayer.PlaybackModes
{
    internal interface IPlaybackContainer : IDisposable
    {
        int CurrentPageElapsedSeconds { get; }
        int CurrentPageDurationSeconds { get; }
        bool IsOnlySinglePage { get; }
        string CurrentPageListName { get; }
        string CurrentPageName { get; }
        string NextPageName { get; }

        void Initialize();
        void StartInitialPlayback(string defaultPlaylist);
        bool IsPresentationActive();
        List<PlaybackDebugItem> GetDebugItems();
        void UpdateCurrentPageListName(string pageListName);
        void RequestScheduleEvaluation(bool force = false);
        void HandleWeeklyScheduleUpdated();
        void RequestPlaylistReload(string playlistName, string reason);
        void StartPlaybackFromOffAir();
        void PlayNextPage();
        void PlayFirstPage();
        void HideAll();
        void StopAll();
    }
}
