import {Component, EventEmitter, Input, OnDestroy, OnInit, Output} from '@angular/core';
import {Subscription} from 'rxjs';
import {Router} from '@angular/router';
import {MusicPlayerService} from '../../../services/music-player.service';

@Component({
  selector: 'app-playlist-card',
  templateUrl: './playlist-card.component.html',
  styleUrls: ['./playlist-card.component.css']
})
export class PlaylistCardComponent implements OnInit, OnDestroy {
  @Input() title: string = "";
  @Input() username: string = "";
  @Input() subtitle: string = "";
  @Input() imageUrl: string = "";
  @Input() hasControls: boolean = false;
  @Input() id: number = 0;
  @Input() tooltip = "";

  @Output() onEdit: EventEmitter<number> = new EventEmitter();
  @Output() onStats: EventEmitter<number> = new EventEmitter();
  @Output() onDelete: EventEmitter<number> = new EventEmitter();
  @Output() onClick: EventEmitter<number> = new EventEmitter();
  @Output() onPlayClick: EventEmitter<number> = new EventEmitter();

  playBtnStyle = {
    'display': 'none'
  }
  pauseBtnStyle = {
    'display': 'block'
  }

  isPlayingThisAlbum: boolean = false;
  playingState: boolean = false;

  state$! : Subscription;
  trackChange$! : Subscription;

  constructor(private router: Router,
              protected musicPlayerService: MusicPlayerService,) {
  }

  ngOnDestroy(): void {
    this.state$.unsubscribe();
    this.trackChange$.unsubscribe();
  }

  ngOnInit(): void {
    this.isPlayingThisAlbum = this.musicPlayerService.getLastPlayedSong()?.albumId == this.id && this.musicPlayerService.getQueueType() === "playlist";
    this.playingState = this.musicPlayerService.getPlayState();

    this.state$ = this.musicPlayerService.playStateChange.subscribe(state => this.playingState = state);
    this.trackChange$ = this.musicPlayerService.trackEvent.subscribe(track =>
      this.isPlayingThisAlbum = track.albumId == this.id && this.musicPlayerService.getQueueType() === "playlist");

  }

  replaceWithPlaceholder() {
    document.getElementById("thumbnail")!.classList.add("playlist-mat-card-placeholder");
  }

  showPlayButton() {
    this.playBtnStyle['display'] = 'block';
  }

  hidePlayButton() {
    this.playBtnStyle['display'] = 'none';
  }

  emitDelete() {
    this.onDelete.emit(this.id);
  }

  emitStats() {
    this.onStats.emit(this.id);
  }

  emitEdit() {
    this.onEdit.emit(this.id);
  }

  getTitle() {
    return this.title || 'Untitled Playlist';
  }
  getAuthor(): string {
    return this.username ? this.username : 'Nepoznato';
  }


  emitClick() {
    this.onClick.emit(this.id);
  }

  openPlaylist() {
    this.onClick.emit(this.id);
  }

  emitPlayClick(e: MouseEvent) {
      e.stopPropagation();
      this.onPlayClick.emit(this.id);
  }
}
