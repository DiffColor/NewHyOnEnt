package kr.co.turtlelab.andowsignage.views;

import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Bitmap.Config;
import android.graphics.Color;
import android.media.MediaPlayer;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.text.TextUtils;
import android.util.Log;
import android.view.View;
import android.widget.ImageView;
import android.widget.ImageView.ScaleType;
import android.widget.RelativeLayout;

import com.nostra13.universalimageloader.core.DisplayImageOptions;
import com.nostra13.universalimageloader.core.ImageLoader;
import com.nostra13.universalimageloader.core.assist.FailReason;
import com.nostra13.universalimageloader.core.assist.ImageScaleType;
import com.nostra13.universalimageloader.core.listener.SimpleImageLoadingListener;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.datamodels.ElementDataModel;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;
import kr.co.turtlelab.andowsignage.datamodels.ScrolltextDataModel;
import kr.co.turtlelab.andowsignage.datamodels.WelcomeDataModel;
import kr.co.turtlelab.andowsignage.playback.PlaybackContracts;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;
import kr.co.turtlelab.andowsignage.tools.Utils;

public class PlaybackSlotView extends RelativeLayout {

    public interface SlotPreparedCallback {
        void onPrepared(PlaybackSlotView view);
    }

    public interface SlotPlaybackReadyCallback {
        void onPlaybackReady(PlaybackSlotView view);
    }

    private static final String TAG = "PlaybackSlotView";
    private static final long IMAGE_CROSSFADE_DURATION_MS = 240L;
    private static final long PREPARE_TIMEOUT_MS = 5000L;
    private static final long PLAYBACK_READY_FALLBACK_MS = 300L;

    private enum VideoOutputState {
        HIDDEN,
        STANDBY,
        ONSCREEN
    }

    private static final String VIDEO_HIDE_STRATEGY_NAME = "z-index";
    private static final float VIDEO_LAYER_STANDBY = 0f;
    private static final float VIDEO_LAYER_ONSCREEN = 1f;
    private static final float IMAGE_LAYER_OVERLAY = 2f;

    private final Context ctx;
    private final Activity act;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final ImageView imgView1;
    private final ImageView imgView2;
    private final TurtleVideoView videoView1;
    private final TurtleVideoView videoView2;
    private final DisplayImageOptions imgOpt;

    private PlaybackContracts.SlotPlan currentPlan = new PlaybackContracts.SlotPlan();
    private PlaybackContracts.SlotState slotState = PlaybackContracts.SlotState.Idle;
    private SlotPreparedCallback pendingPreparedCallback;
    private SlotPlaybackReadyCallback pendingPlaybackReadyCallback;
    private Runnable prepareTimeoutRunnable;
    private Runnable playbackReadyFallbackRunnable;
    private Runnable videoTransitionFallbackRunnable;

    private int stateGeneration = 0;
    private int videoTransitionSerial = 0;
    private int currentItemIndex = 0;
    private long currentItemElapsedMilliseconds = 0L;
    private long currentItemDurationMilliseconds = 1000L;
    private boolean slotActive = false;
    private boolean initialPrepared = false;
    private boolean preparedContentShown = false;
    private boolean preparedPlaybackStarted = false;
    private boolean playbackReadyNotified = false;
    private long layoutStartElapsedRealtimeMs = 0L;
    private int contentRenderWidth = 1;
    private int contentRenderHeight = 1;
    private String videoView1Path = "";
    private String videoView2Path = "";
    private boolean videoView1Prepared = false;
    private boolean videoView2Prepared = false;
    private VideoOutputState videoView1OutputState = VideoOutputState.HIDDEN;
    private VideoOutputState videoView2OutputState = VideoOutputState.HIDDEN;
    private long videoView1StandbyStartedAtElapsedRealtimeMs = 0L;
    private long videoView2StandbyStartedAtElapsedRealtimeMs = 0L;
    private long videoView1StartRequestedAtElapsedRealtimeMs = 0L;
    private long videoView2StartRequestedAtElapsedRealtimeMs = 0L;
    private long lastVideoHiddenToRenderLatencyMs = -1L;
    private long lastVideoStartToRenderLatencyMs = -1L;
    private String lastVideoTransitionSummary = "";

    private final int[] targetIndexHolder = new int[1];
    private final long[] elapsedHolder = new long[1];

    public PlaybackSlotView(Activity act, Context context) {
        super(context);
        this.act = act;
        this.ctx = context;

        LayoutParams params = new LayoutParams(LayoutParams.MATCH_PARENT, LayoutParams.MATCH_PARENT);
        params.addRule(RelativeLayout.CENTER_IN_PARENT, RelativeLayout.TRUE);

        imgView1 = new ImageView(ctx);
        imgView1.setVisibility(View.GONE);

        imgView2 = new ImageView(ctx);
        imgView2.setVisibility(View.GONE);

        videoView1 = new TurtleVideoView(ctx);
        videoView1.setVisibility(View.GONE);

        videoView2 = new TurtleVideoView(ctx);
        videoView2.setVisibility(View.GONE);

        if (AndoWSignageApp.KEEP_ASPECT_RATIO) {
            imgView1.setScaleType(ScaleType.FIT_CENTER);
            imgView2.setScaleType(ScaleType.FIT_CENTER);
            videoView1.setKeepAspectRatio(true);
            videoView2.setKeepAspectRatio(true);
        } else {
            imgView1.setScaleType(ScaleType.FIT_XY);
            imgView2.setScaleType(ScaleType.FIT_XY);
            videoView1.setKeepAspectRatio(false);
            videoView2.setKeepAspectRatio(false);
        }

        addView(videoView1, params);
        addView(videoView2, params);
        addView(imgView1, params);
        addView(imgView2, params);
        applyImageOverlayZOrder(imgView1);
        applyImageOverlayZOrder(imgView2);
        applyVideoViewZOrder(videoView1, false);
        applyVideoViewZOrder(videoView2, false);

        imgOpt = new DisplayImageOptions.Builder()
                .bitmapConfig(Config.ARGB_8888)
                .cacheInMemory(false)
                .cacheOnDisk(true)
                .resetViewBeforeLoading(false)
                .imageScaleType(ImageScaleType.EXACTLY)
                .build();

        setViewEvents();
        releaseSlot();
    }

    public static String getVideoHideStrategyName() {
        return VIDEO_HIDE_STRATEGY_NAME;
    }

    private void setViewEvents() {
        setVideoViewEvents(videoView1);
        setVideoViewEvents(videoView2);
    }

    private void setVideoViewEvents(final TurtleVideoView targetView) {
        if (targetView == null) {
            return;
        }
        targetView.setOnCompletionListener(new MediaPlayer.OnCompletionListener() {
            @Override
            public void onCompletion(MediaPlayer mp) {
                if (slotState == PlaybackContracts.SlotState.Preparing) {
                    finishPrepare(false);
                    return;
                }
                if (slotState == PlaybackContracts.SlotState.Active) {
                    if (restartSingleVideoSlotPlaybackIfNeeded(targetView)) {
                        return;
                    }
                    reapplyPlaybackPositionSoon();
                }
            }
        });

        targetView.setOnErrorListener(new MediaPlayer.OnErrorListener() {
            @Override
            public boolean onError(MediaPlayer mp, int what, int extra) {
                if (slotState == PlaybackContracts.SlotState.Preparing) {
                    finishPrepare(false);
                } else {
                    stopVideoPlayback(targetView);
                    slotState = PlaybackContracts.SlotState.Error;
                }
                return true;
            }
        });
    }

    public void configureMediaSlot(ElementDataModel element, List<MediaDataModel> mediaContents) {
        PlaybackContracts.SlotPlan slotPlan = buildSlotPlan(element, mediaContents);
        configureSlot(slotPlan);
    }

    public void configureTemplateSlot(ElementDataModel element, List<MediaDataModel> mediaContents) {
        configureMediaSlot(element, mediaContents);
    }

    public void configureScrollSlot(ElementDataModel element, List<ScrolltextDataModel> scrolltextContents) {
        deactivateSlot();
    }

    public void configureWelcomeSlot(ElementDataModel element, WelcomeDataModel welcomeData) {
        deactivateSlot();
    }

    public void deactivateSlot() {
        configureSlot(null);
    }

    public void configureSlot(PlaybackContracts.SlotPlan slotPlan) {
        stateGeneration++;
        cancelPrepareTimeout();
        cancelPlaybackReadyFallback();
        cancelVideoTransitionFallback();
        stopAllVideoPlayback();
        hideAllImageOverlays();
        pendingPreparedCallback = null;
        pendingPlaybackReadyCallback = null;
        currentPlan = slotPlan != null ? slotPlan : new PlaybackContracts.SlotPlan();
        slotActive = false;
        initialPrepared = false;
        preparedContentShown = false;
        preparedPlaybackStarted = false;
        playbackReadyNotified = false;
        layoutStartElapsedRealtimeMs = 0L;
        currentItemIndex = 0;
        currentItemElapsedMilliseconds = 0L;
        currentItemDurationMilliseconds = getItemDurationMilliseconds(0);
        slotState = PlaybackContracts.SlotState.Idle;
        applySlotPlan();
        setVisibility(currentPlan.hasPlayableItems() ? View.VISIBLE : View.INVISIBLE);
    }

    public void setLayoutStartElapsedRealtimeMs(long startedAtElapsedRealtimeMs) {
        layoutStartElapsedRealtimeMs = Math.max(0L, startedAtElapsedRealtimeMs);
    }

    public void prepareInitialContent(SlotPreparedCallback callback) {
        pendingPreparedCallback = callback;
        cancelPrepareTimeout();
        cancelPlaybackReadyFallback();
        playbackReadyNotified = false;
        preparedPlaybackStarted = false;
        preparedContentShown = false;
        slotActive = false;
        currentItemIndex = 0;
        currentItemElapsedMilliseconds = 0L;
        currentItemDurationMilliseconds = getItemDurationMilliseconds(0);

        if (!hasPlayableItems()) {
            initialPrepared = true;
            slotState = PlaybackContracts.SlotState.Ready;
            notifyPrepared();
            return;
        }

        initialPrepared = false;
        slotState = PlaybackContracts.SlotState.Preparing;
        final int generation = stateGeneration;
        schedulePrepareTimeout(generation);
        PlaybackContracts.ContentItem currentItem = getCurrentItem();
        PlaybackContracts.ContentItem nextItem = getNextItem();
        if (currentItem == null) {
            finishPrepare(false);
            return;
        }
        if (currentItem.isVideo()) {
            prepareInitialVideo(currentItem, nextItem, generation);
        } else {
            prepareInitialImage(currentItem, nextItem, generation);
        }
    }

    public void showPreparedContent() {
        if (preparedContentShown) {
            return;
        }
        if (!hasPlayableItems()) {
            setVisibility(View.INVISIBLE);
            preparedContentShown = true;
            return;
        }
        preparedContentShown = true;
        setVisibility(View.VISIBLE);
        PlaybackContracts.ContentItem currentItem = getCurrentItem();
        if (currentItem == null) {
            return;
        }
        if (currentItem.isVideo()) {
            showPreparedVideoFrame();
        } else {
            showPreparedImage();
        }
    }

    public void startPreparedPlayback() {
        startPreparedPlayback(null);
    }

    public void startPreparedPlayback(SlotPlaybackReadyCallback callback) {
        if (preparedPlaybackStarted) {
            if (callback != null) {
                callback.onPlaybackReady(this);
            }
            return;
        }

        pendingPlaybackReadyCallback = callback;
        playbackReadyNotified = false;
        cancelPlaybackReadyFallback();
        preparedPlaybackStarted = true;
        slotActive = true;

        if (!hasPlayableItems()) {
            slotState = PlaybackContracts.SlotState.Active;
            notifyPlaybackReady();
            return;
        }

        setVisibility(View.VISIBLE);
        slotState = PlaybackContracts.SlotState.Active;
        currentItemIndex = 0;
        currentItemElapsedMilliseconds = 0L;
        currentItemDurationMilliseconds = getItemDurationMilliseconds(0);
        PlaybackContracts.ContentItem currentItem = getCurrentItem();
        if (currentItem == null) {
            notifyPlaybackReady();
            return;
        }
        if (currentItem.isVideo()) {
            startPreparedVideoPlayback();
        } else {
            notifyContentActuallyPresented(getVisibleImageView(), new Runnable() {
                @Override
                public void run() {
                    notifyPlaybackReady();
                }
            });
        }
    }

    public void stopPlayback() {
        stateGeneration++;
        cancelPrepareTimeout();
        cancelPlaybackReadyFallback();
        cancelVideoTransitionFallback();
        slotActive = false;
        initialPrepared = false;
        preparedContentShown = false;
        preparedPlaybackStarted = false;
        playbackReadyNotified = false;
        layoutStartElapsedRealtimeMs = 0L;
        pendingPreparedCallback = null;
        pendingPlaybackReadyCallback = null;
        currentItemIndex = 0;
        currentItemElapsedMilliseconds = 0L;
        currentItemDurationMilliseconds = 1000L;
        stopAllVideoPlayback();
        hideAllImageOverlays();
        slotState = PlaybackContracts.SlotState.Idle;
        setVisibility(View.INVISIBLE);
    }

    public void pausePlayback() {
        slotActive = false;
        cancelPlaybackReadyFallback();
        cancelVideoTransitionFallback();
        pauseVideoPlayback(videoView1);
        pauseVideoPlayback(videoView2);
        stopVideoInfoCallback(videoView1);
        stopVideoInfoCallback(videoView2);
        hideAllImageOverlays();
        hideVideoSurface(videoView1);
        hideVideoSurface(videoView2);
        slotState = hasPlayableItems() ? PlaybackContracts.SlotState.Ready : PlaybackContracts.SlotState.Idle;
        setVisibility(View.INVISIBLE);
    }

    public void releaseSlot() {
        currentPlan = new PlaybackContracts.SlotPlan();
        stopPlayback();
        applySlotPlan();
    }

    public void tick() {
        if (!slotActive || layoutStartElapsedRealtimeMs <= 0L) {
            return;
        }
        applyPlaybackPosition(SystemClock.elapsedRealtime() - layoutStartElapsedRealtimeMs);
    }

    public void nextContent() {
        if (!hasPlayableItems()) {
            return;
        }
        currentItemIndex = normalizeIndex(currentItemIndex + 1);
        currentItemElapsedMilliseconds = 0L;
        currentItemDurationMilliseconds = getItemDurationMilliseconds(currentItemIndex);
        presentCurrentItem(false);
    }

    public void prevContent() {
        if (!hasPlayableItems()) {
            return;
        }
        currentItemIndex = normalizeIndex(currentItemIndex - 1);
        currentItemElapsedMilliseconds = 0L;
        currentItemDurationMilliseconds = getItemDurationMilliseconds(currentItemIndex);
        presentCurrentItem(false);
    }

    public boolean isMediaSlot() {
        return hasPlayableItems();
    }

    public boolean hasPlayableItems() {
        return currentPlan != null && currentPlan.hasPlayableItems();
    }

    public PlaybackContracts.SlotState getSlotState() {
        return slotState;
    }

    public String getElementName() {
        return currentPlan != null ? currentPlan.elementName : "";
    }

    public boolean isCurrentContentVideo() {
        PlaybackContracts.ContentItem item = getCurrentItem();
        return item != null && item.isVideo();
    }

    public void applyPlaybackPosition(long layoutElapsedMilliseconds) {
        if (!slotActive || !hasPlayableItems()) {
            return;
        }

        resolvePlaybackPosition(layoutElapsedMilliseconds, targetIndexHolder, elapsedHolder);
        int targetIndex = targetIndexHolder[0];
        long itemElapsedMilliseconds = elapsedHolder[0];
        currentItemElapsedMilliseconds = itemElapsedMilliseconds;
        currentItemDurationMilliseconds = getItemDurationMilliseconds(targetIndex);

        if (targetIndex == currentItemIndex) {
            updateCurrentItemLoopState(false);
            return;
        }

        currentItemIndex = targetIndex;
        presentCurrentItem(false);
    }

    private void resolvePlaybackPosition(long layoutElapsedMilliseconds, int[] targetIndexOut, long[] elapsedOut) {
        int targetIndex = 0;
        long itemElapsedMilliseconds = 0L;
        if (hasPlayableItems()) {
            long cycleDurationMilliseconds = getCycleDurationMilliseconds();
            long cycleElapsedMilliseconds = Math.max(0L, layoutElapsedMilliseconds);
            if (cycleDurationMilliseconds > 0L) {
                cycleElapsedMilliseconds %= cycleDurationMilliseconds;
            }
            long cumulative = 0L;
            for (int i = 0; i < currentPlan.items.size(); i++) {
                long itemDurationMilliseconds = getItemDurationMilliseconds(i);
                if (cycleElapsedMilliseconds < cumulative + itemDurationMilliseconds) {
                    targetIndex = i;
                    itemElapsedMilliseconds = cycleElapsedMilliseconds - cumulative;
                    break;
                }
                cumulative += itemDurationMilliseconds;
                targetIndex = Math.max(0, currentPlan.items.size() - 1);
                itemElapsedMilliseconds = Math.max(0L, getItemDurationMilliseconds(targetIndex) - 1L);
            }
        }
        targetIndexOut[0] = targetIndex;
        elapsedOut[0] = itemElapsedMilliseconds;
    }

    public PlaybackContracts.SlotSyncStatus getSyncStatus() {
        if (!hasPlayableItems()) {
            return null;
        }
        PlaybackContracts.ContentItem currentItem = getCurrentItem();
        PlaybackContracts.ContentItem nextItem = getNextItem();
        PlaybackContracts.SlotSyncStatus status = new PlaybackContracts.SlotSyncStatus();
        status.elementName = getElementName();
        status.currentContentName = currentItem != null ? safeString(currentItem.fileName) : "";
        status.nextContentName = nextItem != null ? safeString(nextItem.fileName) : "";
        status.currentIndex = currentItemIndex;
        status.nextIndex = getNextIndex();
        status.elapsedSeconds = currentItemElapsedMilliseconds / 1000L;
        status.durationSeconds = Math.max(1L, currentItemDurationMilliseconds / 1000L);
        status.isVisible = isSlotVisible();
        return status;
    }

    public String getVideoStrategyDebugSummary() {
        if (!hasPlayableItems()) {
            return getElementName() + " strategy=" + getVideoHideStrategyName() + " idle";
        }
        StringBuilder builder = new StringBuilder();
        builder.append(getElementName())
                .append(" strategy=").append(getVideoHideStrategyName())
                .append(" on=").append(debugFileName(getVideoViewPath(getOnScreenVideoView())))
                .append(" standby=").append(debugFileName(getVideoViewPath(getStandbyVideoView())));
        if (!TextUtils.isEmpty(lastVideoTransitionSummary)) {
            builder.append(" last=").append(lastVideoTransitionSummary);
        }
        return builder.toString();
    }

    public void reapplyVideoHideStrategy() {
        if (!hasPlayableItems()) {
            return;
        }
        restoreVisibleOutputs();
    }

    public boolean shouldDelayLayoutTransition() {
        if (!slotActive || !hasPlayableItems() || currentPlan.items.size() <= 1) {
            return false;
        }
        return Math.max(0L, currentItemDurationMilliseconds - currentItemElapsedMilliseconds) > 0L;
    }

    public boolean isPlaybackActiveForHeartbeat() {
        if (!slotActive || !hasPlayableItems() || getVisibility() != View.VISIBLE) {
            return false;
        }
        if (isVideoPlaying(videoView1)) {
            return true;
        }
        return getVisibleImageView() != null;
    }

    private PlaybackContracts.SlotPlan buildSlotPlan(ElementDataModel element, List<MediaDataModel> mediaContents) {
        PlaybackContracts.SlotPlan slotPlan = new PlaybackContracts.SlotPlan();
        if (element != null) {
            slotPlan.elementName = safeString(element.getName());
            slotPlan.width = Math.max(1, element.getWidth());
            slotPlan.height = Math.max(1, element.getHeight());
            slotPlan.left = Math.max(0, element.getX());
            slotPlan.top = Math.max(0, element.getY());
            try {
                slotPlan.zIndex = Integer.parseInt(element.getZorder());
            } catch (Exception ignored) {
                slotPlan.zIndex = 0;
            }
            contentRenderWidth = slotPlan.width;
            contentRenderHeight = slotPlan.height;
        }

        List<MediaDataModel> contents = mediaContents != null ? mediaContents : new ArrayList<MediaDataModel>();
        for (MediaDataModel source : contents) {
            PlaybackContracts.ContentItem item = buildContentItem(source);
            if (item != null) {
                slotPlan.isMuted = source == null || source.isMuted();
                slotPlan.items.add(item);
            }
        }
        return slotPlan;
    }

    private PlaybackContracts.ContentItem buildContentItem(MediaDataModel source) {
        if (source == null) {
            return null;
        }
        String filePath = normalizeLocalVideoPath(source.getFilePath());
        String fileName = safeString(source.getFileName());
        if (TextUtils.isEmpty(filePath) && TextUtils.isEmpty(fileName)) {
            return null;
        }

        AndoWSignageApp.CONTENT_TYPE contentType = parseContentType(source.getType());
        if (contentType == AndoWSignageApp.CONTENT_TYPE.None) {
            return null;
        }

        long durationSeconds = Math.max(1L, source.getPlayTimeSec());
        if (durationSeconds <= 0L) {
            return null;
        }

        if (!source.getValidState()) {
            return null;
        }

        File file = resolvePlayableFile(filePath);
        if (file == null || !file.exists() || file.length() <= 0L) {
            return null;
        }

        PlaybackContracts.ContentItem item = new PlaybackContracts.ContentItem();
        item.source = source;
        item.fileName = TextUtils.isEmpty(fileName) ? file.getName() : fileName;
        item.filePath = file.getAbsolutePath();
        item.contentType = contentType;
        item.durationSeconds = durationSeconds;
        item.actualDurationSeconds = contentType == AndoWSignageApp.CONTENT_TYPE.Video
                ? getActualVideoDurationSeconds(file.getAbsolutePath(), durationSeconds)
                : durationSeconds;
        applyLoopPolicy(item);
        return item;
    }

    private void applyLoopPolicy(PlaybackContracts.ContentItem item) {
        if (item == null || !item.isVideo()) {
            item.shouldLoop = false;
            item.transitionByTimer = true;
            item.loopDisableAfterEndCount = 0;
            item.transitionEndEventCount = 0;
            return;
        }

        long configuredSeconds = Math.max(1L, item.durationSeconds);
        long actualSeconds = Math.max(1L, item.actualDurationSeconds);

        if (configuredSeconds < actualSeconds) {
            item.shouldLoop = false;
            item.transitionByTimer = true;
            item.loopDisableAfterEndCount = 0;
            item.transitionEndEventCount = 0;
            return;
        }

        if (configuredSeconds == actualSeconds) {
            item.shouldLoop = false;
            item.transitionByTimer = false;
            item.loopDisableAfterEndCount = 0;
            item.transitionEndEventCount = 1;
            return;
        }

        long quotient = configuredSeconds / actualSeconds;
        long remainder = configuredSeconds % actualSeconds;
        item.shouldLoop = true;
        item.transitionByTimer = remainder != 0L;
        item.loopDisableAfterEndCount = (int) Math.max(0L, quotient - 1L);
        item.transitionEndEventCount = item.transitionByTimer ? 0 : 1;
    }

    private AndoWSignageApp.CONTENT_TYPE parseContentType(String type) {
        if (TextUtils.isEmpty(type)) {
            return AndoWSignageApp.CONTENT_TYPE.None;
        }
        try {
            return AndoWSignageApp.CONTENT_TYPE.valueOf(type);
        } catch (Exception ignored) {
            return AndoWSignageApp.CONTENT_TYPE.None;
        }
    }

    private File resolvePlayableFile(String filePath) {
        if (TextUtils.isEmpty(filePath)) {
            return null;
        }
        File file = new File(filePath);
        if (file.exists()) {
            return file;
        }
        if (!TextUtils.isEmpty(new File(filePath).getName())) {
            File fallback = new File(LocalPathUtils.getContentPath(new File(filePath).getName()));
            if (fallback.exists()) {
                return fallback;
            }
        }
        return file;
    }

    private long getActualVideoDurationSeconds(String filePath, long fallbackSeconds) {
        try {
            long durationMs = Utils.getVideoDuration(ctx, filePath);
            if (durationMs > 0L) {
                return Math.max(1L, (durationMs + 999L) / 1000L);
            }
        } catch (Exception ex) {
            Log.w(TAG, "getActualVideoDurationSeconds: fallback. path=" + filePath, ex);
        }
        return Math.max(1L, fallbackSeconds);
    }

    private void applySlotPlan() {
        LayoutParams params = (LayoutParams) getLayoutParams();
        if (params == null) {
            params = new LayoutParams(1, 1);
        }
        params.width = Math.max(1, currentPlan.width);
        params.height = Math.max(1, currentPlan.height);
        params.leftMargin = Math.max(0, currentPlan.left);
        params.topMargin = Math.max(0, currentPlan.top);
        setLayoutParams(params);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            setZ(currentPlan.zIndex);
        }
    }

    private void schedulePrepareTimeout(final int generation) {
        cancelPrepareTimeout();
        prepareTimeoutRunnable = new Runnable() {
            @Override
            public void run() {
                if (generation != stateGeneration || slotState != PlaybackContracts.SlotState.Preparing) {
                    return;
                }
                finishPrepare(false);
            }
        };
        mainHandler.postDelayed(prepareTimeoutRunnable, PREPARE_TIMEOUT_MS);
    }

    private void cancelPrepareTimeout() {
        if (prepareTimeoutRunnable != null) {
            mainHandler.removeCallbacks(prepareTimeoutRunnable);
            prepareTimeoutRunnable = null;
        }
    }

    private void finishPrepare(boolean success) {
        cancelPrepareTimeout();
        if (!hasPlayableItems()) {
            slotState = PlaybackContracts.SlotState.Ready;
            initialPrepared = true;
        } else {
            slotState = success ? PlaybackContracts.SlotState.Ready : PlaybackContracts.SlotState.Error;
            initialPrepared = success;
            if (!success) {
                stopAllVideoPlayback();
                hideAllImageOverlays();
            }
        }
        notifyPrepared();
    }

    private void notifyPrepared() {
        SlotPreparedCallback callback = pendingPreparedCallback;
        pendingPreparedCallback = null;
        if (callback != null) {
            callback.onPrepared(this);
        }
    }

    private void notifyPlaybackReady() {
        if (playbackReadyNotified) {
            return;
        }
        playbackReadyNotified = true;
        cancelPlaybackReadyFallback();
        SlotPlaybackReadyCallback callback = pendingPlaybackReadyCallback;
        pendingPlaybackReadyCallback = null;
        if (callback != null) {
            callback.onPlaybackReady(this);
        }
    }

    private void schedulePlaybackReadyFallback() {
        cancelPlaybackReadyFallback();
        playbackReadyFallbackRunnable = new Runnable() {
            @Override
            public void run() {
                notifyPlaybackReady();
            }
        };
        mainHandler.postDelayed(playbackReadyFallbackRunnable, PLAYBACK_READY_FALLBACK_MS);
    }

    private void cancelPlaybackReadyFallback() {
        if (playbackReadyFallbackRunnable != null) {
            mainHandler.removeCallbacks(playbackReadyFallbackRunnable);
            playbackReadyFallbackRunnable = null;
        }
    }

    private void scheduleVideoTransitionFallback(final TurtleVideoView targetVideoView,
                                                 final TurtleVideoView previousVideoView,
                                                 final ImageView overlay,
                                                 final PlaybackContracts.ContentItem nextItem,
                                                 final boolean notifyReady,
                                                 final int transitionSerial) {
        cancelVideoTransitionFallback();
        videoTransitionFallbackRunnable = new Runnable() {
            @Override
            public void run() {
                completeVideoTransition(targetVideoView, previousVideoView, overlay, nextItem, notifyReady, transitionSerial, false);
            }
        };
        mainHandler.postDelayed(videoTransitionFallbackRunnable, PLAYBACK_READY_FALLBACK_MS);
    }

    private void cancelVideoTransitionFallback() {
        if (videoTransitionFallbackRunnable != null) {
            mainHandler.removeCallbacks(videoTransitionFallbackRunnable);
            videoTransitionFallbackRunnable = null;
        }
    }

    private void prepareInitialImage(final PlaybackContracts.ContentItem currentItem,
                                     final PlaybackContracts.ContentItem nextItem,
                                     final int generation) {
        restoreVisibleOutputs();
        stopAllVideoPlayback();
        imgView1.animate().cancel();
        imgView1.setAlpha(0f);
        imgView1.setVisibility(View.VISIBLE);
        imgView1.setTag(currentItem.filePath);
        displayImageAtRenderSize(imgView1, currentItem.filePath, new SimpleImageLoadingListener() {
            @Override
            public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
                if (generation != stateGeneration || slotState != PlaybackContracts.SlotState.Preparing) {
                    return;
                }
                preloadNextContentIfNeeded(nextItem, imgView2, getHiddenVideoView(null));
                finishPrepare(true);
            }

            @Override
            public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                if (generation != stateGeneration || slotState != PlaybackContracts.SlotState.Preparing) {
                    return;
                }
                finishPrepare(false);
            }
        });
    }

    private void prepareInitialVideo(final PlaybackContracts.ContentItem currentItem,
                                     final PlaybackContracts.ContentItem nextItem,
                                     final int generation) {
        restoreVisibleOutputs();
        hideAllImageOverlays();
        String normalizedPath = normalizeLocalVideoPath(currentItem.filePath);
        if (!isPlayableLocalVideo(normalizedPath)) {
            finishPrepare(false);
            return;
        }
        stopAllVideoPlayback();
        final TurtleVideoView currentVideoView = videoView1;
        prepareVideoView(currentVideoView, normalizedPath, true, new Runnable() {
            @Override
            public void run() {
                if (generation != stateGeneration || slotState != PlaybackContracts.SlotState.Preparing) {
                    return;
                }
                preloadNextContentIfNeeded(nextItem,
                        imgView1 == getVisibleImageView() ? imgView2 : imgView1,
                        getHiddenVideoView(currentVideoView));
                finishPrepare(true);
            }
        }, new Runnable() {
            @Override
            public void run() {
                if (generation != stateGeneration || slotState != PlaybackContracts.SlotState.Preparing) {
                    return;
                }
                finishPrepare(false);
            }
        });
    }

    private void showPreparedImage() {
        restoreVisibleOutputs();
        ImageView visible = isPreloadedImageView(imgView1, getCurrentItemPath()) ? imgView1 : imgView2;
        if (visible == null) {
            visible = imgView1;
        }
        ImageView hidden = visible == imgView1 ? imgView2 : imgView1;
        hideImageOverlay(hidden);
        visible.setAlpha(1f);
        visible.setVisibility(View.VISIBLE);
        visible.bringToFront();
    }

    private void showPreparedVideoFrame() {
        restoreVisibleOutputs();
        hideAllImageOverlays();
        TurtleVideoView currentVideoView = findPreparedVideoView(getCurrentItemPath(), null);
        if (currentVideoView == null) {
            currentVideoView = getOnScreenVideoView();
        }
        if (currentVideoView == null) {
            currentVideoView = videoView1;
        }
        showVideoOnScreen(currentVideoView);
        TurtleVideoView hiddenVideoView = getHiddenVideoView(currentVideoView);
        if (hasPreparedVideo(hiddenVideoView)) {
            showStandbyVideoView(hiddenVideoView);
        } else {
            hideVideoSurface(hiddenVideoView);
        }
        try {
            currentVideoView.pause();
            currentVideoView.seekTo(1);
        } catch (Exception ignored) {
        }
    }

    private void startPreparedVideoPlayback() {
        TurtleVideoView currentVideoView = findPreparedVideoView(getCurrentItemPath(), null);
        if (currentVideoView == null) {
            currentVideoView = getOnScreenVideoView();
        }
        if (currentVideoView == null) {
            notifyPlaybackReady();
            return;
        }
        startVideoTransition(currentVideoView, currentVideoView, getVisibleImageView(), getNextItem(), true);
    }

    private void presentCurrentItem(boolean notifyReady) {
        PlaybackContracts.ContentItem currentItem = getCurrentItem();
        PlaybackContracts.ContentItem nextItem = getNextItem();
        if (currentItem == null) {
            return;
        }
        preparedContentShown = true;
        preparedPlaybackStarted = true;
        if (currentItem.isVideo()) {
            showVideoWithImageFade(currentItem, nextItem, notifyReady);
        } else {
            final boolean shouldNotifyReady = notifyReady;
            final boolean fadeOverVideo = getOnScreenVideoView() != null;
            showImageWithCrossfade(currentItem.filePath,
                    getVisibleImageView() == null && !fadeOverVideo,
                    nextItem,
                    fadeOverVideo,
                    new Runnable() {
                        @Override
                        public void run() {
                            if (shouldNotifyReady) {
                                notifyPlaybackReady();
                            }
                        }
                    });
        }
        slotState = PlaybackContracts.SlotState.Active;
    }

    private void showImageWithCrossfade(final String filePath,
                                        boolean immediate,
                                        final PlaybackContracts.ContentItem nextItem,
                                        final boolean fadeOverVideo,
                                        final Runnable endAction) {
        restoreVisibleOutputs();
        final ImageView currentView = getVisibleImageView();
        final ImageView preloadedView = findPreloadedImageView(filePath, currentView);
        final ImageView nextView = preloadedView != null ? preloadedView : getHiddenImageView(currentView);
        final TurtleVideoView currentVideoView = getOnScreenVideoView();
        final TurtleVideoView nextVideoView = getHiddenVideoView(currentVideoView);
        final ImageView nextImagePreloadView = currentView != null ? currentView : getHiddenImageView(nextView);
        if (nextView == null) {
            if (endAction != null) {
                endAction.run();
            }
            return;
        }

        nextView.animate().cancel();
        nextView.setVisibility(View.GONE);

        final boolean shouldImmediate = immediate || (currentView == null && !fadeOverVideo);
        boolean alreadyLoaded = isPreloadedImageView(nextView, filePath);

        if (alreadyLoaded) {
            if (shouldImmediate) {
                nextView.setAlpha(1f);
                nextView.setVisibility(View.VISIBLE);
                if (currentView != null) {
                    currentView.setVisibility(View.GONE);
                }
                notifyContentActuallyPresented(nextView, new Runnable() {
                    @Override
                    public void run() {
                        if (fadeOverVideo) {
                            stopVideoPlayback(currentVideoView);
                        }
                        preloadNextContentIfNeeded(nextItem, nextImagePreloadView, nextVideoView);
                        if (endAction != null) {
                            endAction.run();
                        }
                    }
                });
                return;
            }

            nextView.post(new Runnable() {
                @Override
                public void run() {
                    nextView.setAlpha(0f);
                    nextView.setVisibility(View.VISIBLE);
                    crossfadeImages(currentView, nextView, IMAGE_CROSSFADE_DURATION_MS, new Runnable() {
                        @Override
                        public void run() {
                            notifyContentActuallyPresented(nextView, new Runnable() {
                                @Override
                                public void run() {
                                    if (fadeOverVideo) {
                                        stopVideoPlayback(currentVideoView);
                                    }
                                    preloadNextContentIfNeeded(nextItem, nextImagePreloadView, nextVideoView);
                                    if (endAction != null) {
                                        endAction.run();
                                    }
                                }
                            });
                        }
                    });
                }
            });
            return;
        }

        nextView.setTag(filePath);
        displayImageAtRenderSize(nextView, filePath, new SimpleImageLoadingListener() {
            @Override
            public void onLoadingComplete(String imageUri, View view, Bitmap loadedImage) {
                nextView.post(new Runnable() {
                    @Override
                    public void run() {
                        if (shouldImmediate) {
                            nextView.setAlpha(1f);
                            nextView.setVisibility(View.VISIBLE);
                            if (currentView != null) {
                                currentView.setVisibility(View.GONE);
                            }
                            notifyContentActuallyPresented(nextView, new Runnable() {
                                @Override
                                public void run() {
                                    if (fadeOverVideo) {
                                        stopVideoPlayback(currentVideoView);
                                    }
                                    preloadNextContentIfNeeded(nextItem, nextImagePreloadView, nextVideoView);
                                    if (endAction != null) {
                                        endAction.run();
                                    }
                                }
                            });
                            return;
                        }
                        nextView.setAlpha(0f);
                        nextView.setVisibility(View.VISIBLE);
                        crossfadeImages(currentView, nextView, IMAGE_CROSSFADE_DURATION_MS, new Runnable() {
                            @Override
                            public void run() {
                                notifyContentActuallyPresented(nextView, new Runnable() {
                                    @Override
                                    public void run() {
                                        if (fadeOverVideo) {
                                            stopVideoPlayback(currentVideoView);
                                        }
                                        preloadNextContentIfNeeded(nextItem, nextImagePreloadView, nextVideoView);
                                        if (endAction != null) {
                                            endAction.run();
                                        }
                                    }
                                });
                            }
                        });
                    }
                });
            }

            @Override
            public void onLoadingFailed(String imageUri, View view, FailReason failReason) {
                nextView.setAlpha(1f);
                nextView.setVisibility(View.VISIBLE);
                if (currentView != null) {
                    currentView.setVisibility(View.GONE);
                }
                if (fadeOverVideo) {
                    stopVideoPlayback(currentVideoView);
                }
                preloadNextContentIfNeeded(nextItem, nextImagePreloadView, nextVideoView);
                notifyContentActuallyPresented(nextView, endAction);
            }
        });
    }

    private void showVideoWithImageFade(final PlaybackContracts.ContentItem currentItem,
                                        final PlaybackContracts.ContentItem nextItem,
                                        final boolean notifyReady) {
        restoreVisibleOutputs();
        final String normalizedPath = normalizeLocalVideoPath(currentItem.filePath);
        if (!isPlayableLocalVideo(normalizedPath)) {
            stopAllVideoPlayback();
            hideAllImageOverlays();
            if (notifyReady) {
                notifyPlaybackReady();
            }
            return;
        }

        ImageView overlayView = getVisibleImageView();
        if (overlayView != null) {
            overlayView.animate().cancel();
            overlayView.setAlpha(1f);
            overlayView.setVisibility(View.VISIBLE);
            overlayView.bringToFront();
        }
        final ImageView overlay = overlayView;

        final TurtleVideoView previousVideoView = getOnScreenVideoView();
        final TurtleVideoView targetVideoView = resolveTargetVideoView(normalizedPath, previousVideoView);
        if (isPreparedVideoView(targetVideoView, normalizedPath)) {
            startVideoTransition(targetVideoView, previousVideoView, overlay, nextItem, notifyReady);
            slotState = PlaybackContracts.SlotState.Active;
            return;
        }
        prepareVideoView(targetVideoView, normalizedPath, false, new Runnable() {
            @Override
            public void run() {
                startVideoTransition(targetVideoView, previousVideoView, overlay, nextItem, notifyReady);
            }
        }, new Runnable() {
            @Override
            public void run() {
                if (notifyReady) {
                    notifyPlaybackReady();
                }
            }
        });
        slotState = PlaybackContracts.SlotState.Active;
    }

    private void startVideoTransition(final TurtleVideoView targetVideoView,
                                      final TurtleVideoView previousVideoView,
                                      final ImageView overlay,
                                      final PlaybackContracts.ContentItem nextItem,
                                      final boolean notifyReady) {
        if (targetVideoView == null) {
            if (notifyReady) {
                notifyPlaybackReady();
            }
            return;
        }
        cancelPlaybackReadyFallback();
        stopVideoInfoCallback(targetVideoView);
        targetVideoView.setMuted(currentPlan.isMuted);
        final int transitionSerial = ++videoTransitionSerial;
        if (targetVideoView == previousVideoView || isVideoViewOnScreen(targetVideoView)) {
            showVideoOnScreen(targetVideoView);
        } else {
            showStandbyVideoView(targetVideoView);
        }
        markVideoStartRequested(targetVideoView);
        targetVideoView.setMediaInfoListener(new MediaPlayer.OnInfoListener() {
            @Override
            public boolean onInfo(MediaPlayer mp, int what, int extra) {
                if (what == MediaPlayer.MEDIA_INFO_VIDEO_RENDERING_START) {
                    completeVideoTransition(targetVideoView, previousVideoView, overlay, nextItem, notifyReady, transitionSerial, true);
                }
                return false;
            }
        });
        updateCurrentItemLoopState(true);
        targetVideoView.start();
        scheduleVideoTransitionFallback(targetVideoView, previousVideoView, overlay, nextItem, notifyReady, transitionSerial);
        if (notifyReady) {
            schedulePlaybackReadyFallback();
        }
    }

    private void completeVideoTransition(final TurtleVideoView targetVideoView,
                                         final TurtleVideoView previousVideoView,
                                         final ImageView overlay,
                                         final PlaybackContracts.ContentItem nextItem,
                                         final boolean notifyReady,
                                         final int transitionSerial,
                                         final boolean fromRenderSignal) {
        if (transitionSerial != videoTransitionSerial) {
            return;
        }
        recordVideoTransitionRendered(targetVideoView, fromRenderSignal);
        videoTransitionSerial++;
        cancelVideoTransitionFallback();
        stopVideoInfoCallback(targetVideoView);
        showVideoOnScreen(targetVideoView);
        final TurtleVideoView releasedVideoView;
        if (previousVideoView != null && previousVideoView != targetVideoView) {
            stopVideoPlayback(previousVideoView);
            releasedVideoView = previousVideoView;
        } else {
            releasedVideoView = null;
        }
        final ImageView nextImageView = overlay != null ? getHiddenImageView(overlay) : getHiddenImageView(getVisibleImageView());
        Runnable afterOverlay = new Runnable() {
            @Override
            public void run() {
                preloadNextContentIfNeeded(nextItem, nextImageView, releasedVideoView);
                if (notifyReady) {
                    notifyPlaybackReady();
                }
            }
        };
        if (overlay != null && overlay.getVisibility() == View.VISIBLE) {
            overlay.animate().cancel();
            overlay.animate()
                    .alpha(0f)
                    .setDuration(IMAGE_CROSSFADE_DURATION_MS)
                    .withEndAction(new Runnable() {
                        @Override
                        public void run() {
                            hideAllImageOverlays();
                            afterOverlay.run();
                        }
                    })
                    .start();
        } else {
            hideAllImageOverlays();
            afterOverlay.run();
        }
    }

    private void prepareVideoView(final TurtleVideoView targetVideoView,
                                  final String normalizedPath,
                                  boolean onScreen,
                                  final Runnable onPreparedAction,
                                  final Runnable onErrorAction) {
        if (targetVideoView == null) {
            if (onErrorAction != null) {
                onErrorAction.run();
            }
            return;
        }
        stopVideoPlayback(targetVideoView);
        targetVideoView.setMuted(currentPlan.isMuted);
        targetVideoView.setLoop(false);
        if (targetVideoView == videoView1 || onScreen) {
            showVideoOnScreen(targetVideoView);
        } else {
            showStandbyVideoView(targetVideoView);
        }
        setVideoViewPath(targetVideoView, normalizedPath);
        setVideoViewPrepared(targetVideoView, false);
        stopVideoInfoCallback(targetVideoView);
        targetVideoView.setOnErrorListener(new MediaPlayer.OnErrorListener() {
            @Override
            public boolean onError(MediaPlayer mp, int what, int extra) {
                if (!TextUtils.equals(normalizedPath, getVideoViewPath(targetVideoView))) {
                    return true;
                }
                stopVideoPlayback(targetVideoView);
                if (onErrorAction != null) {
                    onErrorAction.run();
                }
                return true;
            }
        });
        targetVideoView.setOnPreparedListener(new MediaPlayer.OnPreparedListener() {
            @Override
            public void onPrepared(MediaPlayer mp) {
                if (!TextUtils.equals(normalizedPath, getVideoViewPath(targetVideoView))) {
                    return;
                }
                setVideoViewPrepared(targetVideoView, true);
                try {
                    targetVideoView.pause();
                    targetVideoView.seekTo(1);
                } catch (Exception ignored) {
                }
                if (onPreparedAction != null) {
                    onPreparedAction.run();
                }
            }
        });
        targetVideoView.setVideoPath(normalizedPath);
    }

    private void stopVideoInfoCallback(TurtleVideoView targetVideoView) {
        if (targetVideoView != null) {
            targetVideoView.setMediaInfoListener(null);
        }
    }

    private void notifyContentActuallyPresented(View anchorView, final Runnable afterPresentation) {
        View targetView = anchorView != null ? anchorView : this;
        targetView.postOnAnimation(new Runnable() {
            @Override
            public void run() {
                if (afterPresentation != null) {
                    afterPresentation.run();
                }
            }
        });
    }

    private void updateCurrentItemLoopState(boolean force) {
        PlaybackContracts.ContentItem item = getCurrentItem();
        boolean shouldLoop = shouldLoopCurrentItem(item, currentItemElapsedMilliseconds);
        videoView1.setLoop(false);
        videoView2.setLoop(false);
        if (item == null || !item.isVideo()) {
            return;
        }
        TurtleVideoView currentVideoView = findVideoViewByPath(getCurrentItemPath());
        if (force || currentVideoView != null) {
            if (currentVideoView != null) {
                if (isSingleVideoSlotPlayback()) {
                    currentVideoView.setLoop(true);
                    return;
                }
                currentVideoView.setLoop(shouldLoop);
            }
        }
    }

    private boolean isSingleVideoSlotPlayback() {
        if (!hasPlayableItems() || currentPlan.items.size() != 1) {
            return false;
        }
        PlaybackContracts.ContentItem currentItem = getCurrentItem();
        return currentItem != null && currentItem.isVideo();
    }

    private boolean restartSingleVideoSlotPlaybackIfNeeded(TurtleVideoView targetVideoView) {
        if (!isSingleVideoSlotPlayback() || targetVideoView == null) {
            return false;
        }
        try {
            targetVideoView.seekTo(0);
            targetVideoView.start();
            return true;
        } catch (Exception ex) {
            Log.w(TAG, "restartSingleVideoSlotPlaybackIfNeeded: restart failed", ex);
            return false;
        }
    }

    private static boolean shouldLoopCurrentItem(PlaybackContracts.ContentItem item, long elapsedMilliseconds) {
        if (item == null || !item.isVideo() || !item.shouldLoop) {
            return false;
        }
        if (item.transitionByTimer) {
            return true;
        }
        long itemDurationMilliseconds = Math.max(1L, item.durationSeconds) * 1000L;
        long actualDurationMilliseconds = Math.max(1L, item.actualDurationSeconds) * 1000L;
        long remainingMilliseconds = Math.max(0L, itemDurationMilliseconds - Math.max(0L, elapsedMilliseconds));
        return remainingMilliseconds > actualDurationMilliseconds;
    }

    private PlaybackContracts.ContentItem getCurrentItem() {
        if (!hasPlayableItems() || currentItemIndex < 0 || currentItemIndex >= currentPlan.items.size()) {
            return null;
        }
        return currentPlan.items.get(currentItemIndex);
    }

    private PlaybackContracts.ContentItem getNextItem() {
        int nextIndex = getNextIndex();
        if (nextIndex < 0 || !hasPlayableItems() || nextIndex >= currentPlan.items.size()) {
            return null;
        }
        return currentPlan.items.get(nextIndex);
    }

    private int getNextIndex() {
        if (!hasPlayableItems()) {
            return -1;
        }
        if (currentPlan.items.size() == 1) {
            return 0;
        }
        return normalizeIndex(currentItemIndex + 1);
    }

    private int normalizeIndex(int index) {
        if (!hasPlayableItems()) {
            return 0;
        }
        int itemCount = currentPlan.items.size();
        int normalized = index % itemCount;
        if (normalized < 0) {
            normalized += itemCount;
        }
        return normalized;
    }

    private long getCycleDurationMilliseconds() {
        if (!hasPlayableItems()) {
            return 0L;
        }
        long duration = 0L;
        for (int i = 0; i < currentPlan.items.size(); i++) {
            duration += getItemDurationMilliseconds(i);
        }
        return duration;
    }

    private long getItemDurationMilliseconds(int index) {
        if (!hasPlayableItems() || index < 0 || index >= currentPlan.items.size()) {
            return 1000L;
        }
        PlaybackContracts.ContentItem item = currentPlan.items.get(index);
        return Math.max(1L, item.durationSeconds) * 1000L;
    }

    private String getCurrentItemPath() {
        PlaybackContracts.ContentItem item = getCurrentItem();
        return item != null ? item.filePath : "";
    }

    private void displayImageAtRenderSize(ImageView targetView, String filePath, SimpleImageLoadingListener listener) {
        if (targetView == null || TextUtils.isEmpty(filePath)) {
            if (listener != null) {
                listener.onLoadingFailed(filePath, targetView, null);
            }
            return;
        }
        ImageLoader.getInstance().displayImage(
                LocalPathUtils.getUriStringFromAbsPath(filePath),
                new SafeImageViewAware(targetView, getTargetDecodeWidth(), getTargetDecodeHeight()),
                imgOpt,
                listener);
    }

    private int getTargetDecodeWidth() {
        if (contentRenderWidth > 0) {
            return contentRenderWidth;
        }
        int width = getWidth();
        if (width > 0) {
            return width;
        }
        width = getMeasuredWidth();
        if (width > 0) {
            return width;
        }
        return AndoWSignageApp.getDeviceWidth();
    }

    private int getTargetDecodeHeight() {
        if (contentRenderHeight > 0) {
            return contentRenderHeight;
        }
        int height = getHeight();
        if (height > 0) {
            return height;
        }
        height = getMeasuredHeight();
        if (height > 0) {
            return height;
        }
        return AndoWSignageApp.getDeviceHeight();
    }

    private void preloadNextContentIfNeeded(PlaybackContracts.ContentItem nextItem,
                                            ImageView targetImageView,
                                            TurtleVideoView targetVideoView) {
        if (nextItem == null) {
            return;
        }
        if (nextItem.contentType == AndoWSignageApp.CONTENT_TYPE.Image) {
            preloadNextImageIfNeeded(nextItem, targetImageView);
            if (targetVideoView != null && targetVideoView != getOnScreenVideoView()) {
                stopVideoPlayback(targetVideoView);
            }
            return;
        }
        if (nextItem.contentType == AndoWSignageApp.CONTENT_TYPE.Video) {
            preloadNextVideoIfNeeded(nextItem, targetVideoView);
        }
    }

    private void preloadNextImageIfNeeded(PlaybackContracts.ContentItem nextItem, ImageView targetView) {
        if (nextItem == null
                || nextItem.contentType != AndoWSignageApp.CONTENT_TYPE.Image
                || TextUtils.isEmpty(nextItem.filePath)
                || targetView == null) {
            return;
        }
        targetView.setAlpha(1f);
        targetView.setVisibility(View.GONE);
        targetView.setTag(nextItem.filePath);
        displayImageAtRenderSize(targetView, nextItem.filePath, null);
    }

    private void preloadNextVideoIfNeeded(PlaybackContracts.ContentItem nextItem, TurtleVideoView targetVideoView) {
        if (nextItem == null
                || nextItem.contentType != AndoWSignageApp.CONTENT_TYPE.Video
                || TextUtils.isEmpty(nextItem.filePath)
                || targetVideoView == null) {
            return;
        }
        String normalizedPath = normalizeLocalVideoPath(nextItem.filePath);
        if (!isPlayableLocalVideo(normalizedPath)) {
            stopVideoPlayback(targetVideoView);
            return;
        }
        if (isPreparedVideoView(targetVideoView, normalizedPath)) {
            showStandbyVideoView(targetVideoView);
            return;
        }
        prepareVideoView(targetVideoView, normalizedPath, false, null, null);
    }

    private void crossfadeImages(final ImageView fromView, final ImageView toView, long durationMs, final Runnable endAction) {
        if (toView == null) {
            if (endAction != null) {
                endAction.run();
            }
            return;
        }
        toView.bringToFront();
        toView.setVisibility(View.VISIBLE);
        toView.setAlpha(0f);

        if (fromView == null || fromView.getVisibility() != View.VISIBLE) {
            toView.animate().alpha(1f).setDuration(durationMs).withEndAction(endAction).start();
            return;
        }

        fromView.setAlpha(1f);
        fromView.animate().cancel();
        toView.animate().cancel();
        fromView.animate()
                .alpha(0f)
                .setDuration(durationMs)
                .withEndAction(new Runnable() {
                    @Override
                    public void run() {
                        fromView.setVisibility(View.GONE);
                        fromView.setAlpha(1f);
                    }
                })
                .start();
        toView.animate().alpha(1f).setDuration(durationMs).withEndAction(endAction).start();
    }

    private ImageView getVisibleImageView() {
        if (imgView1.getVisibility() == View.VISIBLE) {
            return imgView1;
        }
        if (imgView2.getVisibility() == View.VISIBLE) {
            return imgView2;
        }
        return null;
    }

    private ImageView getHiddenImageView(ImageView visibleView) {
        if (visibleView == imgView1) {
            return imgView2;
        }
        if (visibleView == imgView2) {
            return imgView1;
        }
        return imgView1;
    }

    private boolean isPreloadedImageView(ImageView view, String filePath) {
        return view != null
                && !TextUtils.isEmpty(filePath)
                && filePath.equals(view.getTag())
                && view.getDrawable() != null;
    }

    private ImageView findPreloadedImageView(String filePath, ImageView excludeView) {
        if (isPreloadedImageView(imgView1, filePath) && imgView1 != excludeView) {
            return imgView1;
        }
        if (isPreloadedImageView(imgView2, filePath) && imgView2 != excludeView) {
            return imgView2;
        }
        return null;
    }

    private void hideAllImageOverlays() {
        hideImageOverlay(imgView1);
        hideImageOverlay(imgView2);
    }

    private void hideImageOverlay(ImageView view) {
        if (view == null) {
            return;
        }
        view.animate().cancel();
        view.setVisibility(View.GONE);
        view.setAlpha(1f);
        view.setBackgroundColor(Color.TRANSPARENT);
    }

    private void stopAllVideoPlayback() {
        cancelPlaybackReadyFallback();
        cancelVideoTransitionFallback();
        stopVideoPlayback(videoView1);
        stopVideoPlayback(videoView2);
    }

    private void stopVideoPlayback(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        try {
            stopVideoInfoCallback(targetVideoView);
            targetVideoView.stopPlayback();
        } catch (Exception ignored) {
        } finally {
            clearVideoViewState(targetVideoView);
            hideVideoSurface(targetVideoView);
        }
    }

    private void pauseVideoPlayback(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        try {
            if (targetVideoView.isPlaying()) {
                targetVideoView.pause();
            }
        } catch (Exception ignored) {
        }
    }

    private boolean isVideoPlaying(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return false;
        }
        try {
            return targetVideoView.isPlaying();
        } catch (Exception ignored) {
            return false;
        }
    }

    private void clearVideoViewState(TurtleVideoView targetVideoView) {
        setVideoViewPrepared(targetVideoView, false);
        setVideoViewPath(targetVideoView, "");
    }

    private void showVideoOnScreen(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        targetVideoView.setVisibility(View.VISIBLE);
        targetVideoView.setAlpha(1f);
        targetVideoView.setTranslationX(0f);
        targetVideoView.setTranslationY(0f);
        applyVideoViewZOrder(targetVideoView, true);
        setVideoViewOutputState(targetVideoView, VideoOutputState.ONSCREEN);
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.LOLLIPOP) {
            targetVideoView.bringToFront();
        }
    }

    private void showStandbyVideoView(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        boolean alreadyStandby = getVideoViewOutputState(targetVideoView) == VideoOutputState.STANDBY
                && targetVideoView.getVisibility() == View.VISIBLE;
        targetVideoView.setVisibility(View.VISIBLE);
        targetVideoView.setAlpha(1f);
        targetVideoView.setTranslationX(0f);
        targetVideoView.setTranslationY(0f);
        applyVideoViewZOrder(targetVideoView, false);
        if (!alreadyStandby) {
            setVideoViewStandbyStartedAt(targetVideoView, SystemClock.elapsedRealtime());
        }
        setVideoViewOutputState(targetVideoView, VideoOutputState.STANDBY);
    }

    private void hideVideoSurface(TurtleVideoView targetVideoView) {
        if (targetVideoView == null) {
            return;
        }
        targetVideoView.setVisibility(View.GONE);
        targetVideoView.setAlpha(1f);
        targetVideoView.setTranslationX(0f);
        targetVideoView.setTranslationY(0f);
        applyVideoViewZOrder(targetVideoView, false);
        setVideoViewOutputState(targetVideoView, VideoOutputState.HIDDEN);
        setVideoViewStandbyStartedAt(targetVideoView, 0L);
        setVideoViewStartRequestedAt(targetVideoView, 0L);
    }

    private void applyVideoViewZOrder(TurtleVideoView targetVideoView, boolean onScreen) {
        if (targetVideoView == null) {
            return;
        }
        try {
            targetVideoView.setZOrderOnTop(false);
            targetVideoView.setZOrderMediaOverlay(onScreen);
        } catch (Exception ignored) {
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            targetVideoView.setZ(onScreen ? VIDEO_LAYER_ONSCREEN : VIDEO_LAYER_STANDBY);
        }
    }

    private void applyImageOverlayZOrder(ImageView targetView) {
        if (targetView == null) {
            return;
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            targetView.setZ(IMAGE_LAYER_OVERLAY);
        }
    }

    private void markVideoStartRequested(TurtleVideoView targetVideoView) {
        setVideoViewStartRequestedAt(targetVideoView, SystemClock.elapsedRealtime());
    }

    private void recordVideoTransitionRendered(TurtleVideoView targetVideoView, boolean fromRenderSignal) {
        if (targetVideoView == null) {
            return;
        }
        long now = SystemClock.elapsedRealtime();
        long standbyStartedAt = getVideoViewStandbyStartedAt(targetVideoView);
        long startRequestedAt = getVideoViewStartRequestedAt(targetVideoView);
        lastVideoHiddenToRenderLatencyMs = standbyStartedAt > 0L ? Math.max(0L, now - standbyStartedAt) : -1L;
        lastVideoStartToRenderLatencyMs = startRequestedAt > 0L ? Math.max(0L, now - startRequestedAt) : -1L;
        lastVideoTransitionSummary = getVideoHideStrategyName()
                + "/" + (fromRenderSignal ? "signal" : "fallback")
                + " hidden=" + formatLatency(lastVideoHiddenToRenderLatencyMs)
                + " start=" + formatLatency(lastVideoStartToRenderLatencyMs)
                + " target=" + debugFileName(getVideoViewPath(targetVideoView));
        Log.i(TAG, "recordVideoTransitionRendered: element=" + getElementName() + " " + lastVideoTransitionSummary);
    }

    private String formatLatency(long value) {
        return value >= 0L ? value + "ms" : "-";
    }

    private boolean isVideoViewOnScreen(TurtleVideoView targetVideoView) {
        return targetVideoView != null
                && targetVideoView.getVisibility() == View.VISIBLE
                && getVideoViewOutputState(targetVideoView) == VideoOutputState.ONSCREEN;
    }

    private TurtleVideoView getOnScreenVideoView() {
        if (isVideoViewOnScreen(videoView1)) {
            return videoView1;
        }
        if (isVideoViewOnScreen(videoView2)) {
            return videoView2;
        }
        return null;
    }

    private TurtleVideoView getStandbyVideoView() {
        if (getVideoViewOutputState(videoView1) == VideoOutputState.STANDBY) {
            return videoView1;
        }
        if (getVideoViewOutputState(videoView2) == VideoOutputState.STANDBY) {
            return videoView2;
        }
        return null;
    }

    private TurtleVideoView getHiddenVideoView(TurtleVideoView visibleVideoView) {
        if (visibleVideoView == videoView1) {
            return videoView2;
        }
        if (visibleVideoView == videoView2) {
            return videoView1;
        }
        TurtleVideoView onScreenVideoView = getOnScreenVideoView();
        if (onScreenVideoView == videoView1) {
            return videoView2;
        }
        if (onScreenVideoView == videoView2) {
            return videoView1;
        }
        if (hasPreparedVideo(videoView1) && !isVideoViewOnScreen(videoView1)) {
            return videoView2;
        }
        return videoView1;
    }

    private TurtleVideoView resolveTargetVideoView(String filePath, TurtleVideoView currentVideoView) {
        TurtleVideoView preparedView = findPreparedVideoView(filePath, currentVideoView);
        if (preparedView != null) {
            return preparedView;
        }
        return getHiddenVideoView(currentVideoView);
    }

    private TurtleVideoView findPreparedVideoView(String filePath, TurtleVideoView excludeView) {
        if (isPreparedVideoView(videoView1, filePath) && videoView1 != excludeView) {
            return videoView1;
        }
        if (isPreparedVideoView(videoView2, filePath) && videoView2 != excludeView) {
            return videoView2;
        }
        return null;
    }

    private TurtleVideoView findVideoViewByPath(String filePath) {
        if (TextUtils.isEmpty(filePath)) {
            return null;
        }
        if (filePath.equals(getVideoViewPath(videoView1))) {
            return videoView1;
        }
        if (filePath.equals(getVideoViewPath(videoView2))) {
            return videoView2;
        }
        return null;
    }

    private boolean hasPreparedVideo(TurtleVideoView targetVideoView) {
        return targetVideoView != null
                && isVideoViewPrepared(targetVideoView)
                && !TextUtils.isEmpty(getVideoViewPath(targetVideoView));
    }

    private boolean isPreparedVideoView(TurtleVideoView targetVideoView, String filePath) {
        return targetVideoView != null
                && !TextUtils.isEmpty(filePath)
                && filePath.equals(getVideoViewPath(targetVideoView))
                && isVideoViewPrepared(targetVideoView);
    }

    private String getVideoViewPath(TurtleVideoView targetVideoView) {
        if (targetVideoView == videoView1) {
            return videoView1Path;
        }
        if (targetVideoView == videoView2) {
            return videoView2Path;
        }
        return "";
    }

    private void setVideoViewPath(TurtleVideoView targetVideoView, String path) {
        if (targetVideoView == videoView1) {
            videoView1Path = safeString(path);
        } else if (targetVideoView == videoView2) {
            videoView2Path = safeString(path);
        }
    }

    private boolean isVideoViewPrepared(TurtleVideoView targetVideoView) {
        if (targetVideoView == videoView1) {
            return videoView1Prepared;
        }
        if (targetVideoView == videoView2) {
            return videoView2Prepared;
        }
        return false;
    }

    private void setVideoViewPrepared(TurtleVideoView targetVideoView, boolean prepared) {
        if (targetVideoView == videoView1) {
            videoView1Prepared = prepared;
        } else if (targetVideoView == videoView2) {
            videoView2Prepared = prepared;
        }
    }

    private VideoOutputState getVideoViewOutputState(TurtleVideoView targetVideoView) {
        if (targetVideoView == videoView1) {
            return videoView1OutputState;
        }
        if (targetVideoView == videoView2) {
            return videoView2OutputState;
        }
        return VideoOutputState.HIDDEN;
    }

    private void setVideoViewOutputState(TurtleVideoView targetVideoView, VideoOutputState state) {
        if (targetVideoView == videoView1) {
            videoView1OutputState = state;
        } else if (targetVideoView == videoView2) {
            videoView2OutputState = state;
        }
    }

    private long getVideoViewStandbyStartedAt(TurtleVideoView targetVideoView) {
        if (targetVideoView == videoView1) {
            return videoView1StandbyStartedAtElapsedRealtimeMs;
        }
        if (targetVideoView == videoView2) {
            return videoView2StandbyStartedAtElapsedRealtimeMs;
        }
        return 0L;
    }

    private void setVideoViewStandbyStartedAt(TurtleVideoView targetVideoView, long value) {
        if (targetVideoView == videoView1) {
            videoView1StandbyStartedAtElapsedRealtimeMs = value;
        } else if (targetVideoView == videoView2) {
            videoView2StandbyStartedAtElapsedRealtimeMs = value;
        }
    }

    private long getVideoViewStartRequestedAt(TurtleVideoView targetVideoView) {
        if (targetVideoView == videoView1) {
            return videoView1StartRequestedAtElapsedRealtimeMs;
        }
        if (targetVideoView == videoView2) {
            return videoView2StartRequestedAtElapsedRealtimeMs;
        }
        return 0L;
    }

    private void setVideoViewStartRequestedAt(TurtleVideoView targetVideoView, long value) {
        if (targetVideoView == videoView1) {
            videoView1StartRequestedAtElapsedRealtimeMs = value;
        } else if (targetVideoView == videoView2) {
            videoView2StartRequestedAtElapsedRealtimeMs = value;
        }
    }

    private String debugFileName(String filePath) {
        if (TextUtils.isEmpty(filePath)) {
            return "-";
        }
        return new File(filePath).getName();
    }

    private void restoreVisibleOutputs() {
        setVisibility(View.VISIBLE);
        setTranslationX(0f);
        setTranslationY(0f);
        restoreVideoViewOutput(videoView1);
        restoreVideoViewOutput(videoView2);
        imgView1.setTranslationX(0f);
        imgView1.setTranslationY(0f);
        imgView2.setTranslationX(0f);
        imgView2.setTranslationY(0f);
    }

    private void restoreVideoViewOutput(TurtleVideoView targetVideoView) {
        if (targetVideoView == null || targetVideoView.getVisibility() != View.VISIBLE) {
            return;
        }
        if (isVideoViewOnScreen(targetVideoView)) {
            showVideoOnScreen(targetVideoView);
            return;
        }
        if (getVideoViewOutputState(targetVideoView) == VideoOutputState.STANDBY) {
            showStandbyVideoView(targetVideoView);
        }
    }

    private boolean isSlotVisible() {
        return getOnScreenVideoView() != null || getVisibleImageView() != null;
    }

    private void reapplyPlaybackPositionSoon() {
        if (!slotActive || layoutStartElapsedRealtimeMs <= 0L) {
            return;
        }
        mainHandler.postDelayed(new Runnable() {
            @Override
            public void run() {
                if (!slotActive || layoutStartElapsedRealtimeMs <= 0L) {
                    return;
                }
                applyPlaybackPosition(SystemClock.elapsedRealtime() - layoutStartElapsedRealtimeMs);
            }
        }, 16L);
    }

    private String normalizeLocalVideoPath(String videoPath) {
        if (TextUtils.isEmpty(videoPath)) {
            return videoPath;
        }
        try {
            android.net.Uri parsed = android.net.Uri.parse(videoPath);
            if ("file".equalsIgnoreCase(parsed.getScheme()) && !TextUtils.isEmpty(parsed.getPath())) {
                return parsed.getPath();
            }
        } catch (Exception ignored) {
        }
        return videoPath;
    }

    private boolean isPlayableLocalVideo(String videoPath) {
        if (TextUtils.isEmpty(videoPath)) {
            return false;
        }
        File file = new File(videoPath);
        return file.exists() && file.length() > 0L;
    }

    private String safeString(String value) {
        return value == null ? "" : value;
    }
}
