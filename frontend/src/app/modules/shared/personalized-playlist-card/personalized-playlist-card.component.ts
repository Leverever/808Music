import {Component, EventEmitter, Input, OnDestroy, OnInit, Output} from '@angular/core';
import {Subscription} from 'rxjs';
import {
  PersonalizedPlaylistSummary
} from '../../../endpoints/personalization-endpoints/personalized-playlists-endpoint.service';
import {MusicPlayerService} from '../../../services/music-player.service';

@Component({
  selector: 'app-personalized-playlist-card',
  templateUrl: './personalized-playlist-card.component.html',
  styleUrls: [
    '../album-card/album-card.component.css',
    './personalized-playlist-card.component.css'
  ]
})
export class PersonalizedPlaylistCardComponent implements OnInit, OnDestroy {
  @Input({required: true}) playlist!: PersonalizedPlaylistSummary;
  @Input() imageUrl = '';

  @Output() onClick = new EventEmitter<string>();
  @Output() onPlayClick = new EventEmitter<string>();

  playButtonStyle = {display: 'none'};
  pauseButtonStyle = {display: 'block'};
  playingState = false;

  private readonly subscriptions: Subscription[] = [];

  constructor(protected musicPlayerService: MusicPlayerService) {}

  ngOnInit(): void {
    this.playingState = this.musicPlayerService.getPlayState();
    this.subscriptions.push(
      this.musicPlayerService.playStateChange.subscribe(state => this.playingState = state),
      this.musicPlayerService.trackEvent.subscribe(() => {
        this.playingState = this.musicPlayerService.getPlayState();
      })
    );
  }

  ngOnDestroy(): void {
    this.subscriptions.forEach(subscription => subscription.unsubscribe());
  }

  isPlayingThisPlaylist(): boolean {
    return this.musicPlayerService.getQueueType() === 'personalized-playlist' &&
      this.musicPlayerService.queueSource.value === this.playlistPath;
  }

  showPlayButton(): void {
    this.playButtonStyle = {display: 'block'};
  }

  hidePlayButton(): void {
    this.playButtonStyle = {display: 'none'};
  }

  openPlaylist(): void {
    this.onClick.emit(this.playlist.id);
  }

  handlePlayClick(event: MouseEvent): void {
    event.stopPropagation();
    if(this.isPlayingThisPlaylist())
    {
      this.musicPlayerService.togglePlayState();
      return;
    }

    this.onPlayClick.emit(this.playlist.id);
  }

  getTitle(): string {
    return this.playlist.name;
  }

  private get playlistPath(): string {
    return `/listener/playlist/daily/${this.playlist.id}`;
  }
}
