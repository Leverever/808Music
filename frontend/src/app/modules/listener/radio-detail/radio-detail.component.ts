import {Component, OnDestroy, OnInit} from '@angular/core';
import {Location} from '@angular/common';
import {ActivatedRoute} from '@angular/router';
import {catchError, EMPTY, forkJoin, Subscription, switchMap} from 'rxjs';
import {
  TrackGetByIdEndpointService,
  TrackGetResponse
} from '../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {TrackRadioEndpointService} from '../../../endpoints/personalization-endpoints/track-radio-endpoint.service';
import {RecommendationTrackMapper} from '../../../services/personalization/recommendation-track.mapper';
import {MusicPlayerService} from '../../../services/music-player.service';
import {MyConfig} from '../../../my-config';

@Component({
  selector: 'app-radio-detail',
  templateUrl: './radio-detail.component.html',
  styleUrls: [
    '../personalized-playlist-detail/personalized-playlist-detail.component.css',
    './radio-detail.component.css'
  ]
})
export class RadioDetailComponent implements OnInit, OnDestroy {
  seedTrack: TrackGetResponse | null = null;
  tracks: TrackGetResponse[] = [];
  loading = true;
  errorMessage = '';

  private routeSubscription?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private trackEndpoint: TrackGetByIdEndpointService,
    private radioEndpoint: TrackRadioEndpointService,
    private mapper: RecommendationTrackMapper,
    private musicPlayerService: MusicPlayerService,
    private location: Location
  ) {}

  ngOnInit(): void {
    this.routeSubscription = this.route.paramMap.pipe(
      switchMap(params => {
        const trackId = Number(params.get('trackId'));
        this.seedTrack = null;
        this.tracks = [];
        this.errorMessage = '';
        this.loading = true;

        if(!Number.isInteger(trackId) || trackId <= 0)
        {
          this.errorMessage = 'This song radio link is invalid.';
          this.loading = false;
          return EMPTY;
        }

        return forkJoin({
          seedTrack: this.trackEndpoint.handleAsync(trackId),
          radio: this.radioEndpoint.handleAsync(trackId)
        }).pipe(
          catchError(error => {
            console.error('Could not load song radio.', error);
            this.errorMessage = 'This song radio could not be loaded.';
            this.loading = false;
            return EMPTY;
          })
        );
      })
    ).subscribe(({seedTrack, radio}) => {
      this.seedTrack = seedTrack;
      this.tracks = this.mapper.toPlayerTracks(radio.tracks);
      this.loading = false;
    });
  }

  ngOnDestroy(): void {
    this.routeSubscription?.unsubscribe();
  }

  playAll(startIndex = 0): void {
    if(this.seedTrack == null || this.tracks.length === 0)
    {
      return;
    }

    const orderedTracks = [...this.tracks.slice(startIndex), ...this.tracks.slice(0, startIndex)];
    this.musicPlayerService.createQueue(
      orderedTracks,
      {
        display: `${this.seedTrack.title} Radio`,
        value: `/listener/radio/${this.seedTrack.id}`
      },
      'radio');
  }

  playTrack(trackId: number): void {
    const startIndex = this.tracks.findIndex(track => track.id === trackId);
    if(startIndex >= 0)
    {
      this.playAll(startIndex);
    }
  }

  getTotalTrackLength(): string {
    const totalSeconds = this.tracks.reduce((total, track) => total + track.length, 0);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}m ${seconds}s`;
  }

  getSeedArtists(): string {
    return this.seedTrack?.artists.map(artist => artist.name).join(', ') || 'Unknown artist';
  }

  coverUrl(): string {
    return this.mediaUrl(this.seedTrack?.coverPath);
  }

  get radioArtworkCss(): string {
    if(!this.seedTrack)
    {
      return 'none';
    }

    return `url("${this.coverUrl()}")`;
  }

  mediaUrl(path?: string): string {
    const value = path || '/media/Images/playlist_placeholder.png';
    if(/^https?:\/\//i.test(value))
    {
      return value;
    }

    const normalizedPath = value.startsWith('/media/')
      ? value
      : `/media/${value.replace(/^\/+/, '')}`;
    return `${MyConfig.api_address}${normalizedPath}`;
  }

  goBack(): void {
    this.location.back();
  }
}
