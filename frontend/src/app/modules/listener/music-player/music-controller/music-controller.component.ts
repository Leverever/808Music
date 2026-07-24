import {Component, EventEmitter, HostListener, OnDestroy, OnInit, Output} from '@angular/core';
import {TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {SecondsToDurationStringPipe} from '../../../../services/pipes/seconds-to-string.pipe';
import {MatSliderDragEvent} from '@angular/material/slider';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {HttpClient} from '@angular/common/http';
import {MyConfig} from '../../../../my-config';
import {MyUserAuthService} from '../../../../services/auth-services/my-user-auth.service';
import {
  PlaybackAssetDto,
  TrackPlaybackManifestEndpointService,
  TrackPlaybackManifestResponse
} from '../../../../endpoints/track-endpoints/track-playback-manifest-endpoint.service';
import {MatSlideToggleChange} from '@angular/material/slide-toggle';
import {PlaybackInteractionTrackerService} from '../../../../services/personalization/playback-interaction-tracker.service';
import {TrackInteractionContext} from '../../../../endpoints/personalization-endpoints/track-interaction-endpoint.service';

export interface StemMix {
  name: string;
  volume: number;
}

interface StemAudioPlayer {
  stem: PlaybackAssetDto;
  audio: HTMLAudioElement;
  sourceNode?: MediaElementAudioSourceNode;
  gainNode?: GainNode;
  timeUpdateHandler?: () => void;
  endedHandler?: () => void;
  loadedMetadataHandler: () => void;
  errorHandler: () => void;
}

@Component({
  selector: 'app-music-controller',
  templateUrl: './music-controller.component.html',
  styleUrl: './music-controller.component.css'
})
export class MusicControllerComponent implements OnInit, OnDestroy {
  @Output() stemMixerRequested = new EventEmitter<void>();
  @Output() skipNextRequested = new EventEmitter<void>();
  @Output() skipPreviousRequested = new EventEmitter<void>();

  jwt: string = "";
  track : TrackGetResponse | null = null;
  trackLocation = `${MyConfig.api_address}/api/TrackStreamEndpoint?TrackId=`;
  masterAudioSource = "";
  secondsPipe = new SecondsToDurationStringPipe();
  currentPlaybackTime: number = 0;
  playingState = false;
  isShuffled = false;
  isLooping = false;
  player : HTMLAudioElement | null = null;
  masterVolume = 0.5;
  private previousMasterVolume = 0.5;
  playbackManifest: TrackPlaybackManifestResponse | null = null;
  playbackManifestLoading = false;
  stemModeRequested = false;
  stemModeActive = false;
  stemUnavailableReason = "";
  stemMixes: StemMix[] = [];

  //Stream counting control vars
  streamCounted = false;
  streamedSec = 0;
  lastStreamIncrement = 0;
  secondsNeeded = 10;

  private availableStemAssets: PlaybackAssetDto[] = [];
  private stemPlayers: StemAudioPlayer[] = [];
  private playbackRequestId = 0;
  private streamIntervalId: number | undefined;
  private usingLegacyMasterStream = true;
  private shouldPlayWhenReady = false;
  private audioContext: AudioContext | null = null;
  private masterGainNode: GainNode | null = null;
  private usingWebAudioMixer = false;
  private webAudioUnavailableReason = "";
  private readonly stemModeStorageKey = "stem-streaming-enabled";
  private readonly stemVolumeStorageKey = "music-stem-volumes";
  private readonly stemSyncThresholdSeconds = 0.08;
  private readonly stemSortOrder = ["Vocals", "Drums", "Bass", "Other", "Instrumental"];
  private pendingPlaybackRestoreTime: number | null = null;

  constructor(private musicPlayerService: MusicPlayerService,
              private httpClient : HttpClient,
              private auth: MyUserAuthService,
              private playbackManifestEndpoint: TrackPlaybackManifestEndpointService,
              private playbackInteractionTracker: PlaybackInteractionTrackerService) {
  }

  ngOnInit(): void {
      this.player = document.getElementById("player") as HTMLAudioElement;
      this.jwt = this.auth.getAuthToken()?.token ?? "";
      this.stemModeRequested = window.localStorage.getItem(this.stemModeStorageKey) === "true";
      let previousVolume = this.clampVolume(Number.parseFloat(window.localStorage.getItem("music-volume") ?? "0.5"));
      this.masterVolume = previousVolume;
      this.previousMasterVolume = previousVolume > 0 ? previousVolume : 0.5;
      if(this.player != null)
      {
        this.player.volume = previousVolume;
      }

      this.track = this.musicPlayerService.getLastPlayedSong();
      if(this.track != null)
      {
        this.currentPlaybackTime = this.musicPlayerService.getPersistedPlaybackTime(this.track.id);
      }

      if(this.track != null)
      {
        this.playbackInteractionTracker.beginTrack(
          this.track.id,
          this.track.length * 1000,
          this.getInteractionContext());
        this.initializeMediaSession();
        this.updateMediaSessionMetadata();
        this.loadPlaybackForTrack(this.track.id, false, this.currentPlaybackTime);
      }

      this.musicPlayerService.trackEvent.subscribe({
        next: value => {
          this.playbackInteractionTracker.beginTrack(
            value.id,
            value.length * 1000,
            this.getInteractionContext(),
            true);
          this.track = value;
          this.streamCounted = false;
          this.streamedSec = 0;
          this.currentPlaybackTime = 0;
          this.musicPlayerService.setPlaybackProgress(0, value.length);
          this.updateMediaSessionMetadata();
          this.loadPlaybackForTrack(value.id, true, 0);
        }
      })

      this.initializeMediaSession();

      this.isShuffled = this.musicPlayerService.isShuffled;

      this.musicPlayerService.shuffleToggled.subscribe({
        next: value => {
          this.isShuffled = value;
        }
      })

      this.musicPlayerService.playStateChange.subscribe(state => {
        if(state != this.playingState)
        {
          this.setPlaybackState(state, false);
        }
      })

      this.streamIntervalId = window.setInterval(() => {
        if(this.playingState)
        {
          let millis = Date.now();
          if(millis - this.lastStreamIncrement >= 1000)
          {
            this.lastStreamIncrement = millis;
            this.streamedSec++;
            if(this.streamedSec >= this.secondsNeeded && !this.streamCounted)
            {
              this.httpClient.post(MyConfig.api_address + "/api/TrackAddStreamEndpoint/" + this.track?.id, {}).subscribe({
                next: value => {
                  console.log("Stream counted");
                },
                error: err => {
                  console.error('Error occurred:', err);

                    console.log('Došlo je do pogreške prilikom dodavanja streama.');

                }
              });

              this.streamCounted = true;
              this.streamedSec = 0;
          }
          }
        }
        else {
          this.streamedSec = 0;
        }
      }, 250);
  }

  ngOnDestroy(): void {
    this.persistCurrentPlaybackPosition();

    if(this.streamIntervalId !== undefined)
    {
      window.clearInterval(this.streamIntervalId);
    }

    this.clearMediaSession();
    this.playbackInteractionTracker.playbackPaused();
    this.teardownStemPlayers();
    void this.audioContext?.close();
  }

  @HostListener('window:keydown.space', ['$event'])
  handleSpacebar(event: KeyboardEvent) {
    if (event.target instanceof HTMLInputElement ||
      event.target instanceof HTMLTextAreaElement ||
      event.target instanceof HTMLSelectElement) {
      return;
    }
    event.preventDefault();
    this.changePlayerState();
  }

  @HostListener('window:keydown.shift.n', ['$event'])
  handleSkipNext(event: KeyboardEvent) {
    if (event.target instanceof HTMLInputElement ||
      event.target instanceof HTMLTextAreaElement ||
      event.target instanceof HTMLSelectElement) {
      return;
    }
    event.preventDefault();
    this.skipNext();
  }

  @HostListener('window:keydown.shift.b', ['$event'])
  handleSkipPrevious(event: KeyboardEvent) {
    if (event.target instanceof HTMLInputElement ||
      event.target instanceof HTMLTextAreaElement ||
      event.target instanceof HTMLSelectElement) {
      return;
    }
    event.preventDefault();
    this.skipPrevious();
  }

  @HostListener('window:keydown.shift.s', ['$event'])
  handleToggleShuffle(event: KeyboardEvent) {
    if (event.target instanceof HTMLInputElement ||
      event.target instanceof HTMLTextAreaElement ||
      event.target instanceof HTMLSelectElement) {
      return;
    }
    event.preventDefault();
    this.setShuffleState();
  }

  @HostListener('window:keydown.shift.r', ['$event'])
  handleToggleRepeat(event: KeyboardEvent) {
    if (event.target instanceof HTMLInputElement ||
      event.target instanceof HTMLTextAreaElement ||
      event.target instanceof HTMLSelectElement) {
      return;
    }
    event.preventDefault();
    this.setLoopState();
  }

  setCurrentPlaybackTime(e: number) {
    this.currentPlaybackTime = e;
    this.musicPlayerService.setPlaybackProgress(this.currentPlaybackTime, this.track?.length ?? 0);
    this.updateMediaSessionPositionState();
  }

  changePlayerState() {
    this.setPlaybackState(!this.playingState);
  }

  setPlaybackState(shouldPlay: boolean, notifyService = true) {
    if(this.playbackManifestLoading && this.getActiveAudioElements().length === 0)
    {
      this.playingState = shouldPlay;
      this.shouldPlayWhenReady = shouldPlay;
      this.updateMediaSessionPlaybackState();

      if(notifyService)
      {
        this.musicPlayerService.setPlayState(this.playingState);
      }

      return;
    }

    if(shouldPlay)
    {
      this.playingState = true;
      this.playActiveAudio();
    }
    else
    {
      this.playingState = false;
      this.pauseAllAudio();
      this.playbackInteractionTracker.playbackPaused();
    }

    this.updateMediaSessionPlaybackState();

    if(notifyService)
    {
      this.musicPlayerService.setPlayState(this.playingState);
    }
  }

  setSliderValue(e?: Event) {
    const activePlayer = this.getPrimaryAudioElement();
    if(activePlayer != null)
    {
      this.currentPlaybackTime = Math.floor(activePlayer.currentTime);
      this.musicPlayerService.setPlaybackProgress(
        this.currentPlaybackTime,
        Number.isFinite(activePlayer.duration) ? activePlayer.duration : (this.track?.length ?? 0));
      this.updateMediaSessionPositionState();
    }
  }

  userSetSlider(event: MatSliderDragEvent | {value: number}) {
    this.seekTo(event.value);
  }

  setVolume(number: number) {
    this.masterVolume = this.clampVolume(number);
    if(this.masterVolume > 0)
    {
      this.previousMasterVolume = this.masterVolume;
    }
    this.applyVolumes();
    window.localStorage.setItem("music-volume", this.masterVolume.toString());
  }

  toggleMute() {
    if(this.masterVolume === 0)
    {
      this.setVolume(this.previousMasterVolume);
      return;
    }

    this.setVolume(0);
  }

  setStemStreamingState(event: MatSlideToggleChange) {
    if(event.checked && this.availableStemAssets.length === 0)
    {
      this.stemModeRequested = false;
      this.stemUnavailableReason = this.playbackManifestLoading
        ? "Stem information is still loading."
        : "Separate stems are not available for this track.";
      return;
    }

    const shouldResume = this.playingState;
    this.stemModeRequested = event.checked;
    window.localStorage.setItem(this.stemModeStorageKey, JSON.stringify(this.stemModeRequested));

    if(this.stemModeRequested)
    {
      this.activateStemMode(shouldResume);
      return;
    }

    this.activateMasterMode(shouldResume);
  }

  requestStemMixer(): void {
    this.stemMixerRequested.emit();
  }

  requestSkipNext(): void {
    this.skipNextRequested.emit();
  }

  requestSkipPrevious(): void {
    this.skipPreviousRequested.emit();
  }

  setStemVolume(stemName: string, volume: number) {
    const mix = this.stemMixes.find(x => x.name === stemName);
    if(mix == null)
    {
      return;
    }

    mix.volume = this.clampVolume(volume);
    this.persistStemVolumes();
    this.applyVolumes();
  }

  setLoopState() {
    this.isLooping = !this.isLooping;
    this.applyLoopState();
  }

  skipNext() {
    if(this.isLooping)
    {
      this.seekTo(0);
      return;
    }

    this.playbackInteractionTracker.skipTrack();

    this.advanceQueue();
  }

  handleTrackEnded() {
    this.playbackInteractionTracker.completeTrack();
    this.advanceQueue();
  }

  private advanceQueue() {

    if(this.playingState)
    {
      this.setPlaybackState(false);
    }

    if (!this.isShuffled) {
      this.musicPlayerService.playNext();
    }
    else {
      this.musicPlayerService.shufflePlay();
    }
  }

  skipPrevious() {
    const currentTime = this.getActiveCurrentTime();
    if(this.isLooping || currentTime > 2)
    {
      this.seekTo(0);
      return;
    }

    this.musicPlayerService.playPrev();
  }

  willNavigateToPreviousTrack(): boolean {
    return !this.isLooping && this.getActiveCurrentTime() <= 2;
  }

  skipToGestureTrack(track: TrackGetResponse): void {
    this.playbackInteractionTracker.skipTrack();

    if(this.playingState)
    {
      this.setPlaybackState(false);
    }

    this.musicPlayerService.playQueuedTrack(track);
  }

  getVolumeSliderValue(event: Event) {
    return Number.parseInt((event.target as HTMLInputElement).value)/100
  }

  setShuffleState() {
    this.musicPlayerService.toggleShuffle();
  }

  getTrackId() {
    return this.track != null ? this.track.id : -1;
  }

  get masterVolumePercent() {
    return Math.round(this.masterVolume * 100);
  }

  get masterVolumeIcon() {
    const volumePercent = this.masterVolumePercent;

    if(volumePercent === 0)
    {
      return "volume_off";
    }

    if(volumePercent < 25)
    {
      return "volume_mute";
    }

    if(volumePercent < 60)
    {
      return "volume_down";
    }

    return "volume_up";
  }

  get hasAvailableStems() {
    return this.availableStemAssets.length > 0;
  }

  get stemToggleDisabled() {
    return this.playbackManifestLoading || !this.hasAvailableStems;
  }

  get stemMenuStatusText() {
    if(this.playbackManifestLoading)
    {
      return "Checking stems...";
    }

    if(this.hasAvailableStems)
    {
      if(this.stemModeActive && this.usingWebAudioMixer)
      {
        return "Streaming separated stems with mixer";
      }

      if(this.stemModeActive && this.webAudioUnavailableReason !== "")
      {
        return "Streaming separated stems";
      }

      return this.stemModeActive ? "Streaming separated stems" : "Streaming master file";
    }

    return this.stemUnavailableReason || "Separate stems are not available for this track.";
  }

  getStemVolumePercent(stemName: string) {
    const mix = this.stemMixes.find(x => x.name === stemName);
    return Math.round((mix?.volume ?? 1) * 100);
  }

  formatStemName(name: string) {
    return name.replace(/([a-z])([A-Z])/g, "$1 $2");
  }

  restoreMasterAudioPosition() {
    if(this.player != null)
    {
      this.restoreAudioPosition(this.player);
    }
  }

  handleMasterPlaybackError() {
    if(this.track == null || this.usingLegacyMasterStream)
    {
      return;
    }

    this.setMasterAudioSource(this.buildLegacyStreamUrl(this.track.id), true);
    if(!this.stemModeActive && this.playingState)
    {
      this.playActiveAudio();
    }
  }

  private loadPlaybackForTrack(trackId: number, playWhenReady: boolean, restoreTime = 0) {
    const requestId = ++this.playbackRequestId;

    this.pendingPlaybackRestoreTime = Math.max(0, restoreTime);
    this.pauseAllAudio();
    this.playingState = playWhenReady;
    this.shouldPlayWhenReady = playWhenReady;
    this.playbackManifest = null;
    this.playbackManifestLoading = true;
    this.stemUnavailableReason = "";
    this.availableStemAssets = [];
    this.stemMixes = [];
    this.stemModeActive = false;
    this.teardownStemPlayers();
    this.setMasterAudioSource("", true);

    this.playbackManifestEndpoint.handleAsync({trackId}).subscribe({
      next: manifest => {
        if(requestId !== this.playbackRequestId)
        {
          return;
        }

        this.playbackManifest = manifest;
        this.playbackManifestLoading = false;
        this.setAvailableStems(manifest.stream.stemSet?.stems ?? []);
        const shouldPlayAfterLoad = this.shouldPlayWhenReady;

        if(this.stemModeRequested && this.hasAvailableStems)
        {
          this.setMasterAudioSource("", false);
          this.activateStemMode(shouldPlayAfterLoad);
          return;
        }

        if(this.stemModeRequested && !this.hasAvailableStems)
        {
          this.stemUnavailableReason = "Separate stems are not available for this track.";
        }

        this.setMasterAudioSource(manifest.stream.master.url, false);
        this.activateMasterMode(shouldPlayAfterLoad);
      },
      error: err => {
        if(requestId !== this.playbackRequestId)
        {
          return;
        }

        console.warn("Playback manifest unavailable; falling back to legacy stream endpoint.", err);
        this.playbackManifestLoading = false;
        this.stemUnavailableReason = "This track is using the legacy stream.";
        this.setAvailableStems([]);
        this.setMasterAudioSource(this.buildLegacyStreamUrl(trackId), true);
        this.activateMasterMode(this.shouldPlayWhenReady);
      }
    });
  }

  private activateStemMode(playWhenReady: boolean) {
    if(!this.hasAvailableStems)
    {
      this.activateMasterMode(playWhenReady);
      return;
    }

    const resumeTime = this.getPlaybackResumeTime();
    this.pauseAllAudio();
    this.setMasterAudioSource("", false);
    this.setupStemPlayers();
    this.stemModeActive = true;
    this.currentPlaybackTime = Math.floor(resumeTime);
    this.seekTo(resumeTime, false);
    this.pendingPlaybackRestoreTime = null;
    this.applyLoopState();
    this.applyVolumes();
    this.updateMediaSessionMetadata();
    this.updateMediaSessionPositionState();

    if(playWhenReady)
    {
      this.setPlaybackState(true);
    }
    this.shouldPlayWhenReady = false;
  }

  private activateMasterMode(playWhenReady: boolean) {
    const resumeTime = this.getPlaybackResumeTime();
    this.pauseStemPlayers();
    this.teardownStemPlayers();
    this.stemModeActive = false;
    this.ensureMasterAudioSource();
    this.currentPlaybackTime = Math.floor(resumeTime);
    this.seekTo(resumeTime, false);
    this.pendingPlaybackRestoreTime = null;
    this.applyLoopState();
    this.applyVolumes();
    this.updateMediaSessionMetadata();
    this.updateMediaSessionPositionState();

    if(playWhenReady)
    {
      this.setPlaybackState(true);
    }
    this.shouldPlayWhenReady = false;
  }

  private setupStemPlayers() {
    this.teardownStemPlayers();
    const shouldUseWebAudioMixer = this.ensureAudioContext() != null;

    this.stemPlayers = this.availableStemAssets.map((stem, index) => {
      const audio = new Audio();
      if(shouldUseWebAudioMixer)
      {
        audio.crossOrigin = "anonymous";
      }
      audio.src = stem.url;
      audio.preload = "auto";
      audio.loop = this.isLooping;

      const loadedMetadataHandler = () => this.restoreAudioPosition(audio);
      const errorHandler = () => this.handleStemPlaybackError(stem.name);
      const player: StemAudioPlayer = {
        stem,
        audio,
        loadedMetadataHandler,
        errorHandler
      };

      if(index === 0)
      {
        player.timeUpdateHandler = () => {
          this.setSliderValue();
          this.syncStemPlayers();
        };
        player.endedHandler = () => this.handleTrackEnded();
        audio.addEventListener("timeupdate", player.timeUpdateHandler);
        audio.addEventListener("ended", player.endedHandler);
      }

      audio.addEventListener("loadedmetadata", loadedMetadataHandler);
      audio.addEventListener("error", errorHandler);
      return player;
    });

    this.usingWebAudioMixer = shouldUseWebAudioMixer && this.connectStemPlayersToMixer();
  }

  private teardownStemPlayers() {
    this.stemPlayers.forEach(stemPlayer => {
      this.disconnectStemMixerNodes(stemPlayer);
      stemPlayer.audio.pause();
      stemPlayer.audio.removeAttribute("src");
      stemPlayer.audio.load();

      if(stemPlayer.timeUpdateHandler != null)
      {
        stemPlayer.audio.removeEventListener("timeupdate", stemPlayer.timeUpdateHandler);
      }

      if(stemPlayer.endedHandler != null)
      {
        stemPlayer.audio.removeEventListener("ended", stemPlayer.endedHandler);
      }

      stemPlayer.audio.removeEventListener("loadedmetadata", stemPlayer.loadedMetadataHandler);
      stemPlayer.audio.removeEventListener("error", stemPlayer.errorHandler);
    });

    this.stemPlayers = [];
    this.usingWebAudioMixer = false;
  }

  private playActiveAudio() {
    const audioElements = this.getActiveAudioElements();
    if(audioElements.length === 0)
    {
      return;
    }

    audioElements.forEach(audio => this.restoreAudioPosition(audio));
    const trackId = this.track?.id;
    this.resumeAudioContext()
      .then(() => Promise.all(audioElements.map(audio => audio.play())))
      .then(() => this.playbackInteractionTracker.playbackStarted(trackId))
      .catch((error: Error) => {
        console.log(error);
      });
  }

  private pauseAllAudio() {
    if(this.player != null)
    {
      this.player.pause();
    }

    this.pauseStemPlayers();
  }

  private pauseStemPlayers() {
    this.stemPlayers.forEach(stemPlayer => stemPlayer.audio.pause());
  }

  private seekTo(value: number, persist = true) {
    const targetTime = Math.max(0, value);

    this.currentPlaybackTime = targetTime;
    this.getActiveAudioElements().forEach(audio => {
      try
      {
        audio.currentTime = targetTime;
      }
      catch
      {
        // Some browsers reject currentTime before metadata is available.
      }
    });

    if(persist)
    {
      this.musicPlayerService.setPlaybackProgress(
        this.currentPlaybackTime,
        this.track?.length ?? 0);
    }

    this.updateMediaSessionPositionState();
  }

  private restoreAudioPosition(audio: HTMLAudioElement) {
    if(this.currentPlaybackTime <= 0)
    {
      return;
    }

    try
    {
      audio.currentTime = this.currentPlaybackTime;
    }
    catch
    {
      // The seek will be retried from loadedmetadata.
    }
  }

  private getActiveAudioElements() {
    if(this.stemModeActive)
    {
      return this.stemPlayers.map(stemPlayer => stemPlayer.audio);
    }

    return this.player != null && this.masterAudioSource !== "" ? [this.player] : [];
  }

  private getPrimaryAudioElement() {
    if(this.stemModeActive)
    {
      return this.stemPlayers[0]?.audio ?? null;
    }

    return this.player;
  }

  private getInteractionContext(): TrackInteractionContext {
    switch(this.musicPlayerService.getQueueType())
    {
      case 'autoplay': return 'Autoplay';
      case 'radio': return 'Radio';
      case 'playlist':
      case 'personalized-playlist': return 'Playlist';
      case 'song': return 'Manual';
      default: return 'Playback';
    }
  }

  private getActiveCurrentTime() {
    const activePlayer = this.getPrimaryAudioElement();
    return activePlayer != null && Number.isFinite(activePlayer.currentTime)
      ? activePlayer.currentTime
      : this.currentPlaybackTime;
  }

  private getPlaybackResumeTime(): number {
    return this.pendingPlaybackRestoreTime ?? this.getActiveCurrentTime();
  }

  @HostListener('window:pagehide')
  private persistCurrentPlaybackPosition(): void {
    if(this.track == null)
    {
      return;
    }

    const currentTime = this.pendingPlaybackRestoreTime ?? this.getActiveCurrentTime();
    if(!Number.isFinite(currentTime))
    {
      return;
    }

    this.currentPlaybackTime = Math.max(0, currentTime);
    const activePlayer = this.getPrimaryAudioElement();
    const duration = activePlayer != null && Number.isFinite(activePlayer.duration)
      ? activePlayer.duration
      : this.track.length;
    this.musicPlayerService.setPlaybackProgress(this.currentPlaybackTime, duration);
  }

  private applyVolumes() {
    if(this.player != null)
    {
      this.player.volume = this.masterVolume;
    }

    if(this.usingWebAudioMixer && this.masterGainNode != null)
    {
      this.masterGainNode.gain.value = this.masterVolume;
    }

    this.stemPlayers.forEach(stemPlayer => {
      const stemVolume = this.stemMixes.find(x => x.name === stemPlayer.stem.name)?.volume ?? 1;
      if(this.usingWebAudioMixer && stemPlayer.gainNode != null)
      {
        stemPlayer.audio.volume = 1;
        stemPlayer.gainNode.gain.value = stemVolume;
        return;
      }

      stemPlayer.audio.volume = this.clampVolume(this.masterVolume * stemVolume);
    });
  }

  private applyLoopState() {
    if(this.player != null)
    {
      this.player.loop = this.isLooping;
    }

    this.stemPlayers.forEach(stemPlayer => {
      stemPlayer.audio.loop = this.isLooping;
    });
  }

  private syncStemPlayers() {
    if(this.stemPlayers.length <= 1)
    {
      return;
    }

    const leader = this.stemPlayers[0].audio;
    this.stemPlayers.slice(1).forEach(stemPlayer => {
      if(Math.abs(stemPlayer.audio.currentTime - leader.currentTime) > this.stemSyncThresholdSeconds)
      {
        stemPlayer.audio.currentTime = leader.currentTime;
      }
    });
  }

  private handleStemPlaybackError(stemName: string) {
    if(!this.stemModeActive)
    {
      return;
    }

    this.stemUnavailableReason = `${this.formatStemName(stemName)} stem could not be loaded.`;
    this.stemModeRequested = false;
    window.localStorage.setItem(this.stemModeStorageKey, JSON.stringify(false));
    this.activateMasterMode(this.playingState);
  }

  private ensureAudioContext() {
    if(this.audioContext != null)
    {
      return this.audioContext;
    }

    const AudioContextConstructor =
      window.AudioContext ??
      (window as typeof window & {webkitAudioContext?: typeof AudioContext}).webkitAudioContext;

    if(AudioContextConstructor == null)
    {
      this.webAudioUnavailableReason = "Web Audio is not available in this browser.";
      return null;
    }

    try
    {
      this.audioContext = new AudioContextConstructor();
      this.masterGainNode = this.audioContext.createGain();
      this.masterGainNode.connect(this.audioContext.destination);
      this.masterGainNode.gain.value = this.masterVolume;
      this.webAudioUnavailableReason = "";
      return this.audioContext;
    }
    catch
    {
      this.webAudioUnavailableReason = "Web Audio could not be started.";
      this.audioContext = null;
      this.masterGainNode = null;
      return null;
    }
  }

  private connectStemPlayersToMixer() {
    const context = this.ensureAudioContext();
    if(context == null || this.masterGainNode == null)
    {
      return false;
    }

    try
    {
      this.stemPlayers.forEach(stemPlayer => {
        const sourceNode = context.createMediaElementSource(stemPlayer.audio);
        const gainNode = context.createGain();

        sourceNode.connect(gainNode);
        gainNode.connect(this.masterGainNode!);

        stemPlayer.sourceNode = sourceNode;
        stemPlayer.gainNode = gainNode;
      });

      return true;
    }
    catch
    {
      this.webAudioUnavailableReason = "Web Audio mixer could not connect to these stems.";
      this.stemPlayers.forEach(stemPlayer => this.disconnectStemMixerNodes(stemPlayer));
      return false;
    }
  }

  private disconnectStemMixerNodes(stemPlayer: StemAudioPlayer) {
    try
    {
      stemPlayer.sourceNode?.disconnect();
      stemPlayer.gainNode?.disconnect();
    }
    catch
    {
      // Nodes may already be disconnected during rapid track changes.
    }

    stemPlayer.sourceNode = undefined;
    stemPlayer.gainNode = undefined;
  }

  private async resumeAudioContext() {
    if(!this.usingWebAudioMixer || this.audioContext == null)
    {
      return;
    }

    if(this.audioContext.state === "suspended")
    {
      await this.audioContext.resume();
    }
  }

  private setAvailableStems(stems: PlaybackAssetDto[]) {
    this.availableStemAssets = this.sortStems(stems);
    const storedStemVolumes = this.getStoredStemVolumes();
    this.stemMixes = this.availableStemAssets.map(stem => ({
      name: stem.name,
      volume: this.clampVolume(storedStemVolumes[stem.name] ?? 1)
    }));
  }

  private sortStems(stems: PlaybackAssetDto[]) {
    return [...stems].sort((left, right) => {
      const leftIndex = this.getStemSortIndex(left.name);
      const rightIndex = this.getStemSortIndex(right.name);

      if(leftIndex === rightIndex)
      {
        return left.name.localeCompare(right.name);
      }

      return leftIndex - rightIndex;
    });
  }

  private getStemSortIndex(stemName: string) {
    const index = this.stemSortOrder.indexOf(stemName);
    return index >= 0 ? index : Number.MAX_SAFE_INTEGER;
  }

  private setMasterAudioSource(source: string, isLegacyStream: boolean) {
    this.masterAudioSource = source;
    this.usingLegacyMasterStream = isLegacyStream;

    if(this.player == null)
    {
      return;
    }

    if(source === "")
    {
      this.player.removeAttribute("src");
      this.player.load();
      return;
    }

    if(this.player.src !== source)
    {
      this.player.src = source;
      this.player.load();
    }

    this.applyVolumes();
    this.updateMediaSessionMetadata();
    this.updateMediaSessionPositionState();
  }

  private initializeMediaSession() {
    const mediaSession = this.getMediaSession();
    if(mediaSession == null)
    {
      return;
    }

    mediaSession.setActionHandler("play", () => this.setPlaybackState(true));
    mediaSession.setActionHandler("pause", () => this.setPlaybackState(false));
    mediaSession.setActionHandler("previoustrack", () => this.skipPrevious());
    mediaSession.setActionHandler("nexttrack", () => this.skipNext());
    mediaSession.setActionHandler("stop", () => this.setPlaybackState(false));
    mediaSession.setActionHandler("seekbackward", details => {
      const offset = details.seekOffset ?? 10;
      this.seekTo(this.getActiveCurrentTime() - offset);
    });
    mediaSession.setActionHandler("seekforward", details => {
      const offset = details.seekOffset ?? 10;
      this.seekTo(this.getActiveCurrentTime() + offset);
    });
    mediaSession.setActionHandler("seekto", details => {
      if(details.seekTime != null)
      {
        if(details.fastSeek)
        {
          const activeAudio = this.getPrimaryAudioElement();
          if(activeAudio != null && typeof activeAudio.fastSeek === "function")
          {
            activeAudio.fastSeek(details.seekTime);
          }
        }

        this.seekTo(details.seekTime);
      }
    });

    this.updateMediaSessionMetadata();
    this.updateMediaSessionPlaybackState();
    this.updateMediaSessionPositionState();
  }

  private clearMediaSession() {
    const mediaSession = this.getMediaSession();
    if(mediaSession == null)
    {
      return;
    }

    ["play", "pause", "previoustrack", "nexttrack", "stop", "seekbackward", "seekforward", "seekto"].forEach(action => {
      mediaSession.setActionHandler(action as MediaSessionAction, null);
    });

    mediaSession.playbackState = "none";
    mediaSession.metadata = null;
  }

  private updateMediaSessionMetadata() {
    const mediaSession = this.getMediaSession();
    if(mediaSession == null || this.track == null)
    {
      return;
    }

    const artistName = this.track.artists.map(artist => artist.name).join(", ") || "808Music";
    const artworkUrl = `${MyConfig.api_address}${this.track.coverPath}`;

    try
    {
      mediaSession.metadata = new MediaMetadata({
        title: this.track.title,
        artist: artistName,
        album: this.stemModeActive ? "Stem playback" : "Music playback",
        artwork: [
          {src: artworkUrl, sizes: "96x96"},
          {src: artworkUrl, sizes: "256x256"},
          {src: artworkUrl, sizes: "512x512"}
        ]
      });
    }
    catch
    {
      // Some browsers expose the API surface inconsistently; ignore metadata failures.
    }
  }

  private updateMediaSessionPlaybackState() {
    const mediaSession = this.getMediaSession();
    if(mediaSession == null)
    {
      return;
    }

    mediaSession.playbackState = this.playingState ? "playing" : "paused";
  }

  private updateMediaSessionPositionState() {
    const mediaSession = this.getMediaSession();
    const activeAudio = this.getPrimaryAudioElement();
    if(mediaSession == null || activeAudio == null || !Number.isFinite(activeAudio.duration) || activeAudio.duration <= 0)
    {
      return;
    }

    try
    {
      mediaSession.setPositionState({
        duration: activeAudio.duration,
        playbackRate: activeAudio.playbackRate || 1,
        position: Math.min(this.currentPlaybackTime, activeAudio.duration)
      });
    }
    catch
    {
      // Ignore browsers that do not support position state updates.
    }
  }

  private getMediaSession() {
    return typeof navigator !== "undefined" && "mediaSession" in navigator
      ? navigator.mediaSession
      : null;
  }

  private buildLegacyStreamUrl(trackId: number) {
    return `${this.trackLocation}${trackId}&Jwt=${encodeURIComponent(this.jwt)}`;
  }

  private ensureMasterAudioSource() {
    if(this.masterAudioSource !== "")
    {
      return;
    }

    if(this.playbackManifest?.stream.master.url != null)
    {
      this.setMasterAudioSource(this.playbackManifest.stream.master.url, false);
      return;
    }

    if(this.track != null)
    {
      this.setMasterAudioSource(this.buildLegacyStreamUrl(this.track.id), true);
    }
  }

  private getStoredStemVolumes() {
    const storedVolumes = window.localStorage.getItem(this.stemVolumeStorageKey);
    if(storedVolumes == null || storedVolumes === "")
    {
      return {} as Record<string, number>;
    }

    try
    {
      return JSON.parse(storedVolumes) as Record<string, number>;
    }
    catch
    {
      return {} as Record<string, number>;
    }
  }

  private persistStemVolumes() {
    const volumes = this.stemMixes.reduce((acc, stem) => {
      acc[stem.name] = stem.volume;
      return acc;
    }, {} as Record<string, number>);

    window.localStorage.setItem(this.stemVolumeStorageKey, JSON.stringify(volumes));
  }

  private clampVolume(volume: number) {
    if(!Number.isFinite(volume))
    {
      return 0.5;
    }

    return Math.min(1, Math.max(0, volume));
  }
}
