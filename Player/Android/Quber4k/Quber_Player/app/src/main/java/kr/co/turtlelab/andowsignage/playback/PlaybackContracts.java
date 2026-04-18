package kr.co.turtlelab.andowsignage.playback;

import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;

public final class PlaybackContracts {

    private PlaybackContracts() {
    }

    public enum LayoutState {
        Idle,
        Preparing,
        Ready,
        Active
    }

    public enum SlotState {
        Idle,
        Preparing,
        Ready,
        Active,
        Error
    }

    public static final class ContentItem {
        public MediaDataModel source;
        public String fileName = "";
        public String filePath = "";
        public AndoWSignageApp.CONTENT_TYPE contentType = AndoWSignageApp.CONTENT_TYPE.None;
        public long durationSeconds = 1L;
        public long actualDurationSeconds = 1L;
        public boolean shouldLoop;
        public boolean transitionByTimer = true;
        public int loopDisableAfterEndCount;
        public int transitionEndEventCount;

        public boolean isVideo() {
            return contentType == AndoWSignageApp.CONTENT_TYPE.Video;
        }
    }

    public static final class SlotPlan {
        public String elementName = "";
        public boolean isMuted = true;
        public int width;
        public int height;
        public int left;
        public int top;
        public int zIndex;
        public final List<ContentItem> items = new ArrayList<>();

        public boolean hasPlayableItems() {
            return !items.isEmpty();
        }
    }

    public static final class PagePlan {
        public String playlistName = "";
        public String pageName = "";
        public double canvasWidth = 1920d;
        public double canvasHeight = 1080d;
        public int durationSeconds = 1;
        public final List<SlotPlan> slots = new ArrayList<>();

        public String getPlanKey() {
            return String.valueOf(playlistName) + "|" + String.valueOf(pageName);
        }
    }

    public static final class SlotSyncStatus {
        public String elementName = "";
        public String currentContentName = "";
        public String nextContentName = "";
        public int currentIndex = -1;
        public int nextIndex = -1;
        public long elapsedSeconds;
        public long durationSeconds = 1L;
        public boolean isVisible;
    }

    public static final class PlaybackPulse {
        public SlotSyncStatus status;
        public boolean isSecondTick;
        public boolean isContentBoundary;
    }
}
