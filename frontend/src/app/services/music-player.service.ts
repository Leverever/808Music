  import {EventEmitter, Injectable} from '@angular/core';
import {TrackGetResponse} from '../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {Subject} from 'rxjs';
import {TrackGetAllEndpointService} from '../endpoints/track-endpoints/track-get-all-endpoint.service';
import {AutoplayRecommendationsEndpointService} from '../endpoints/personalization-endpoints/autoplay-recommendations-endpoint.service';
import {RecommendationTrackMapper} from './personalization/recommendation-track.mapper';

export interface QueueSource {
  display: string;
  value: string;
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
  private trackAddEvent = new Subject<TrackGetResponse>();
  trackAdd = this.trackAddEvent.asObservable();
  private shuffleToggleEvent = new Subject<boolean>();
  shuffleToggled = this.shuffleToggleEvent.asObservable();
  isShuffled : boolean = false;
  private autoPlay = true;
  private playingState = false;
  private playStateChangeEvent = new Subject<boolean>();
  playStateChange = this.playStateChangeEvent.asObservable();
  private queueType = "";
  private recentTrackIds: number[] = this.loadRecentTrackIds();
  private autoplayRequestInFlight = false;

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

    window.localStorage.setItem("queue", JSON.stringify({queue, source, type}));
  }

  addToQueue(queueTrack : TrackGetResponse) {
      this.queue.push(queueTrack);
      this.trackAddEvent.next(queueTrack);
  }

  removeFromQueue(queueTrack : TrackGetResponse) {
    let i = this.queue.indexOf(queueTrack);
    if(i > -1 && !this.playedIndexes.includes(i)) {
      this.queue.splice(i, 1);
      this.trackAddEvent.next(queueTrack);
    }
  }

  getQueue() {
    return this.queue.filter((t,i) => !this.playedIndexes.includes(i) || i > this.playedIndexes[this.playedIndexes.length - 1]);
  }

  playNext() {
    if(this.playedIndexes.length == 0)
    {
      this.trackPlayEvent.next(this.queue[0]);
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
    this.trackPlayEvent.next(this.queue[i]);
  }

  playPrev() {
    if(this.playedIndexes.length <= 1)
    {
      return;
    }
    let i = this.playedIndexes.pop()!;
    i = this.playedIndexes.pop()!;
    this.playedIndexes.push(i);
    this.trackPlayEvent.next(this.queue[i]);
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
    this.trackPlayEvent.next(this.queue[i]);
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
      this.trackPlayEvent.next(this.queue[index]);
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
}
