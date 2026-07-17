import {Component, EventEmitter, Input, OnDestroy, OnInit, Output} from '@angular/core';
import {Router} from '@angular/router';
import {Subscription} from 'rxjs';
import {MusicPlayerService} from '../../../services/music-player.service';

@Component({
  selector: 'app-album-card',
  templateUrl: './album-card.component.html',
  styleUrl: './album-card.component.css'
})
export class AlbumCardComponent implements OnInit, OnDestroy {
  @Input() title: string = "";
  @Input() subtitle: string = "";
  @Input() imageUrl: string = "";
  @Input() hasControls: boolean = false;
  @Input() id: number = 0;
  @Input() tooltip = "";
  @Input() artistName = "";
  @Input() numOfTracks = -1;
  @Input() artistId: number = 1;
  @Input() role: string = "";
  @Input() isHighLighted : boolean = false;
  @Input() releaseDate: number = 0;

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
    this.isPlayingThisAlbum = this.musicPlayerService.getLastPlayedSong()?.albumId == this.id && this.musicPlayerService.getQueueType() === "album";
    this.playingState = this.musicPlayerService.getPlayState();

    this.state$ = this.musicPlayerService.playStateChange.subscribe(state => this.playingState = state);
    this.trackChange$ = this.musicPlayerService.trackEvent.subscribe(track =>
      this.isPlayingThisAlbum = track.albumId == this.id && this.musicPlayerService.getQueueType() === "album");

  }

  replaceWithPlaceholder() {
    document.getElementById("thumbnail")!.classList.add("album-mat-card-placeholder");
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
    return this.title;
  }

  emitClick() {
    this.onClick.emit(this.id);
  }

  openCardOnMobile(event: MouseEvent): void {
    if(typeof window === 'undefined' || !window.matchMedia('(max-width: 960px)').matches)
    {
      return;
    }

    const target = event.target as HTMLElement | null;
    if(target?.closest('button, .album-mat-card-title, .artist-name'))
    {
      return;
    }

    this.emitClick();
  }

  showPlayButton() {
    this.playBtnStyle['display'] = 'block';
  }

  hidePlayButton() {
    this.playBtnStyle['display'] = 'none';
  }

  emitPlayClick() {
    this.onPlayClick.emit(this.id);
  }

  goToArtist() {
    this.router.navigate(['/listener/profile', this.artistId])
  }

  protected readonly Date = Date;
}
