import {Injectable} from '@angular/core';
import {TrackGetResponse} from '../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {BehaviorSubject, Subject} from 'rxjs';
import {TrackGetAllEndpointService} from '../endpoints/track-endpoints/track-get-all-endpoint.service';
import {AutoplayRecommendationsEndpointService} from '../endpoints/personalization-endpoints/autoplay-recommendations-endpoint.service';
import {RecommendationTrackMapper} from './personalization/recommendation-track.mapper';

export interface QueueSource {
  display: string;
  value: string;
}

export interface PlaybackProgress {
  currentTime: number;
  duration: number;
}

interface PersistedPlaybackPosition extends PlaybackProgress {
  trackId: number;
}

@Injectable({
  providedIn: 'root'
})
export class MusicPlayerService {
  queueSource: QueueSource = {display:"Song", value:"song"};
  queue : TrackGetResponse[] = []
  private playedIndexes : number[] = []
  private trackPlayEvent = new Subject<TrackGetResponse>();
  trackEvent = this.trackPlayEvent.asObservable();
  private currentTrackState = new BehaviorSubject<TrackGetResponse | null>(null);
  currentTrack$ = this.currentTrackState.asObservable();
  private trackAddEvent = new Subject<TrackGetResponse>();
  trackAdd = this.trackAddEvent.asObservable();
  private queuePresenceState = new BehaviorSubject<boolean>(false);
  queuePresence$ = this.queuePresenceState.asObservable();
  private shuffleToggleEvent = new Subject<boolean>();
  shuffleToggled = this.shuffleToggleEvent.asObservable();
  isShuffled : boolean = false;
  private autoPlay = true;
  private playingState = false;
  private playStateChangeEvent = new BehaviorSubject<boolean>(false);
  playStateChange = this.playStateChangeEvent.asObservable();
  private playbackProgressState = new BehaviorSubject<PlaybackProgress>({currentTime: 0, duration: 0});
  playbackProgress$ = this.playbackProgressState.asObservable();
  private queueType = "";
  private recentTrackIds: number[] = this.loadRecentTrackIds();
  private autoplayRequestInFlight = false;
  private readonly playbackPositionStorageKey = "music-playback-position";

  constructor(
    private trackGetAllEndpointService: TrackGetAllEndpointService,
    private autoplayRecommendationsEndpoint: AutoplayRecommendationsEndpointService,
    private recommendationTrackMapper: RecommendationTrackMapper
  ) {
    let lastQueue = window.localStorage.getItem("queue");
    let playedIndexes = this.getCachedPlayedIndexes();
    if(lastQueue != null && lastQueue !== "")
    {
      this.playedIndexes = playedIndexes;
      const {queue, source, type} = JSON.parse(lastQueue);
      this.createQueue(queue, source, type, false, true);
    }

    const lastPlayedTrack = this.getLastPlayedSong();
    if(lastPlayedTrack != null)
    {
      this.currentTrackState.next(lastPlayedTrack);
      this.playbackProgressState.next({
        currentTime: this.getPersistedPlaybackTime(lastPlayedTrack.id),
        duration: lastPlayedTrack.length
      });
    }

    this.trackEvent.subscribe({
      next: (e) => {
        window.localStorage.setItem("lastPlayedSong", JSON.stringify(e));
        window.localStorage.setItem("playedIndexes", JSON.stringify(this.playedIndexes));
        this.recentTrackIds.push(e.id);
        this.recentTrackIds = this.recentTrackIds.slice(-50);
        window.localStorage.setItem("recentTrackIds", JSON.stringify(this.recentTrackIds));
      }
    })

    this.autoPlay = this.getAutoPlayStatus();
  }

  getAutoPlayStatus() {
    return window.localStorage.getItem("autoPlay") === "true";
  }

  setAutoPlayStatus(status: boolean) {
    window.localStorage.setItem("autoPlay", JSON.stringify(status));
    this.autoPlay = status;
  }

  getLastPlayedSong() : TrackGetResponse | null{
    let track = window.localStorage.getItem("lastPlayedSong");
    if(track != null)
    {
      return JSON.parse(track);
    }
    else {
      return null;
    }
  }

  getCachedPlayedIndexes() : number[] {
    let playedIndexes = window.localStorage.getItem("playedIndexes");
    if(playedIndexes != null)
    {
      let indexes = JSON.parse(playedIndexes) as number[];
      if(indexes.length === 0)
      {
        indexes.push(0);
      }
      return indexes;
    }
    else {
      return [];
    }
  }

  createQueue(queue : TrackGetResponse[], source : QueueSource = {display:"Song", value:"song"}, type="album", append : boolean = false, cacheRequest = false) {
    if(queue.length === 0)
    {
      if(!append)
      {
        this.clearQueue();
      }
      return;
    }

    if(!append || this.queue.length == 0) {
      this.queue = queue;
      this.queueType=type;
      this.playedIndexes = cacheRequest ? this.getCachedPlayedIndexes() : [];
      if(!cacheRequest)
      {
        if(!this.isShuffled)
        {
          this.playNext();
        }
        else
        {
          this.shufflePlay();
        }
      }
      this.queueSource = source;
    }
    else {
      this.queue.push(...queue);
      this.trackAddEvent.next(queue[0]);
    }

    this.queuePresenceState.next(this.queue.length > 0);
    window.localStorage.setItem("queue", JSON.stringify({queue, source, type}));
  }

  addToQueue(queueTrack : TrackGetResponse) {
      this.queue.push(queueTrack);
      this.queuePresenceState.next(true);
      this.trackAddEvent.next(queueTrack);
  }

  removeFromQueue(queueTrack : TrackGetResponse) {
    let i = this.queue.indexOf(queueTrack);
    if(i > -1 && !this.playedIndexes.includes(i)) {
      this.queue.splice(i, 1);
      this.queuePresenceState.next(this.queue.length > 0);
      this.trackAddEvent.next(queueTrack);
    }
  }

  clearQueue(): void {
    this.queue = [];
    this.playedIndexes = [];
    this.queuePresenceState.next(false);
    this.setPlayState(false);
    window.localStorage.removeItem("queue");
  }

  getQueue() {
    return this.queue.filter((t,i) => !this.playedIndexes.includes(i) || i > this.playedIndexes[this.playedIndexes.length - 1]);
  }

  reorderUpcomingQueue(previousIndex: number, currentIndex: number): TrackGetResponse[] {
    const upcomingIndexes = this.queue
      .map((_, index) => index)
      .filter(index =>
        !this.playedIndexes.includes(index) ||
        index > this.playedIndexes[this.playedIndexes.length - 1]
      );

    if(
      previousIndex < 0 ||
      currentIndex < 0 ||
      previousIndex >= upcomingIndexes.length ||
      currentIndex >= upcomingIndexes.length ||
      previousIndex === currentIndex
    )
    {
      return this.getQueue();
    }

    const reorderedTracks = upcomingIndexes.map(index => this.queue[index]);
    const [movedTrack] = reorderedTracks.splice(previousIndex, 1);
    reorderedTracks.splice(currentIndex, 0, movedTrack);

    upcomingIndexes.forEach((queueIndex, index) => {
      this.queue[queueIndex] = reorderedTracks[index];
    });

    this.cacheQueue();
    return this.getQueue();
  }

  playNext() {
    if(this.playedIndexes.length == 0)
    {
      if(this.queue.length === 0)
      {
        return;
      }

      this.emitTrack(this.queue[0]);
      this.playedIndexes.push(0);
      return;
    }

    let i = this.playedIndexes[this.playedIndexes.length - 1] + 1;
    if(this.queue.length <= i)
    {
      if(this.autoPlay)
      {
        this.setAutoPlayQueue();
      }
      return;
    }
    this.playedIndexes.push(i);
    this.emitTrack(this.queue[i]);
  }

  playPrev() {
    if(this.playedIndexes.length <= 1)
    {
      return;
    }
    let i = this.playedIndexes.pop()!;
    i = this.playedIndexes.pop()!;
    this.playedIndexes.push(i);
    this.emitTrack(this.queue[i]);
  }

  getPreviousTrack(): TrackGetResponse | null {
    if(this.playedIndexes.length <= 1)
    {
      return null;
    }

    const previousIndex = this.playedIndexes[this.playedIndexes.length - 2];
    return this.queue[previousIndex] ?? null;
  }

  getNextTrackForGesture(): TrackGetResponse | null {
    if(this.queue.length === 0)
    {
      return null;
    }

    if(this.isShuffled)
    {
      const unplayedTracks = this.queue.filter((_, index) => !this.playedIndexes.includes(index));
      if(unplayedTracks.length === 0)
      {
        return null;
      }

      return unplayedTracks[this.getRandomInt(0, unplayedTracks.length)] ?? null;
    }

    const currentIndex = this.playedIndexes[this.playedIndexes.length - 1] ?? -1;
    return this.queue[currentIndex + 1] ?? null;
  }

  playQueuedTrack(track: TrackGetResponse): void {
    const index = this.queue.indexOf(track);
    if(index < 0)
    {
      return;
    }

    this.playedIndexes.push(index);
    this.emitTrack(track);
  }

  shufflePlay() {
    let i = this.getRandomInt(0, this.queue.length);
    let attempts = 1;
    while(this.playedIndexes.includes(i))
    {
      i = this.getRandomInt(0, this.queue.length);
      attempts++;
      if(attempts > this.queue.length)
      {
        this.setAutoPlayQueue();
        return;
      }
    }
    this.playedIndexes.push(i);
    this.emitTrack(this.queue[i]);
  }

  skipTo(track : TrackGetResponse) {
    let index = this.queue.indexOf(track);
    if(index > -1)
    {
      for(let i = 0; i < index; i++)
      {
        this.playedIndexes.push(i);
      }
      this.playedIndexes.push(index);
      this.emitTrack(this.queue[index]);
    }
  }

  getRandomInt(min :number, max:number) {
    const minCeiled = Math.ceil(min);
    const maxFloored = Math.floor(max);
    return Math.floor(Math.random() * (maxFloored - minCeiled) + minCeiled); // The maximum is exclusive and the minimum is inclusive
  }

  toggleShuffle() {
    this.isShuffled = !this.isShuffled;
    this.shuffleToggleEvent.next(this.isShuffled);
  }

  private setAutoPlayQueue() {
    if(this.autoplayRequestInFlight)
    {
      return;
    }

    const seedTrackIds = [...new Set(this.recentTrackIds.slice(-10))];
    if(seedTrackIds.length === 0)
    {
      this.setFallbackAutoPlayQueue();
      return;
    }

    this.autoplayRequestInFlight = true;
    const excludedTrackIds = [...new Set([
      ...this.recentTrackIds,
      ...this.queue.map(track => track.id)
    ])];

    this.autoplayRecommendationsEndpoint.handleAsync({
      seedTrackIds,
      excludedTrackIds,
      limit: 25
    }).subscribe({
      next: response => {
        this.autoplayRequestInFlight = false;
        const tracks = this.recommendationTrackMapper.toPlayerTracks(response.tracks);
        if(tracks.length === 0)
        {
          this.setFallbackAutoPlayQueue();
          return;
        }

        this.createQueue(
          tracks,
          {display: "Recommended Autoplay", value: "/listener/home"},
          "autoplay");
      },
      error: error => {
        console.warn("Personalized autoplay was unavailable; using catalog fallback.", error);
        this.autoplayRequestInFlight = false;
        this.setFallbackAutoPlayQueue();
      }
    });
  }

  private setFallbackAutoPlayQueue() {
    if(this.autoplayRequestInFlight)
    {
      return;
    }

    this.autoplayRequestInFlight = true;
    let sortByStreams = Date.now()%2 == 0;
    this.trackGetAllEndpointService.handleAsync({title:"", isReleased: true, sortByStreams: sortByStreams, pageSize: 1000, pageNumber:1, }).subscribe({
      next: data => {
        this.autoplayRequestInFlight = false;
        if(data.dataItems.length > 0)
        {
          this.createQueue(data.dataItems, {display: sortByStreams ? "808 Popular - Autoplay" : "808 Fresh - Autoplay", value: "/listener/home"}, "autoplay")
        }
      },
      error: () => this.autoplayRequestInFlight = false
    });
  }

  private loadRecentTrackIds(): number[] {
    try
    {
      const stored = JSON.parse(window.localStorage.getItem("recentTrackIds") ?? "[]");
      return Array.isArray(stored)
        ? stored.filter(value => Number.isInteger(value) && value > 0).slice(-50)
        : [];
    }
    catch
    {
      return [];
    }
  }

  getCurrentTrack(): TrackGetResponse | null {
    return this.currentTrackState.value;
  }

  getPlaybackProgress(): PlaybackProgress {
    return this.playbackProgressState.value;
  }

  getPersistedPlaybackTime(trackId: number): number {
    const storedPosition = window.localStorage.getItem(this.playbackPositionStorageKey);
    if(storedPosition != null)
    {
      try
      {
        const parsed = JSON.parse(storedPosition) as Partial<PersistedPlaybackPosition>;
        if(parsed.trackId !== trackId)
        {
          return 0;
        }

        return Number.isFinite(parsed.currentTime)
          ? Math.max(0, Number(parsed.currentTime))
          : 0;
      }
      catch
      {
        // Fall through to the legacy value so existing sessions can be migrated.
      }
    }

    const lastPlayedTrack = this.getLastPlayedSong();
    if(lastPlayedTrack?.id !== trackId)
    {
      return 0;
    }

    const legacyPosition = Number.parseFloat(
      window.localStorage.getItem("currentPlaybackTime") ?? "0");
    return Number.isFinite(legacyPosition) ? Math.max(0, legacyPosition) : 0;
  }

  setPlaybackProgress(currentTime: number, duration: number): void {
    const normalizedProgress = {
      currentTime: Number.isFinite(currentTime) ? Math.max(0, currentTime) : 0,
      duration: Number.isFinite(duration) ? Math.max(0, duration) : 0
    };

    this.playbackProgressState.next(normalizedProgress);

    const currentTrack = this.currentTrackState.value ?? this.getLastPlayedSong();
    if(currentTrack == null)
    {
      return;
    }

    window.localStorage.setItem(
      "currentPlaybackTime",
      normalizedProgress.currentTime.toString());
    window.localStorage.setItem(this.playbackPositionStorageKey, JSON.stringify({
      trackId: currentTrack.id,
      ...normalizedProgress
    } satisfies PersistedPlaybackPosition));
  }

  setPlayState(state: boolean) {
    this.playingState = state;
    this.playStateChangeEvent.next(state);
  }

  togglePlayState() {
    this.playingState = !this.playingState;
    this.playStateChangeEvent.next(this.playingState);
  }

  getPlayState() {
    return this.playingState;
  }

  getQueueType() {
    return this.queueType;
  }

  private cacheQueue(): void {
    window.localStorage.setItem("queue", JSON.stringify({
      queue: this.queue,
      source: this.queueSource,
      type: this.queueType
    }));
  }

  private emitTrack(track: TrackGetResponse): void {
    this.currentTrackState.next(track);
    this.playbackProgressState.next({currentTime: 0, duration: track.length});
    this.trackPlayEvent.next(track);
  }
}
