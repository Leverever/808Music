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
    'display': 'none',
    'bottom': '7vh'
  }

  pauseBtnStyle = {
    'display': 'block',
    'bottom': '7vh'
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

    if(this.artistName != ""){
      this.playBtnStyle['bottom'] = '10vh';
      this.pauseBtnStyle['bottom'] = '10vh';
    }
    else {
      this.playBtnStyle['bottom'] = '7vh';
      this.pauseBtnStyle['bottom'] = '7vh';
    }
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

  showPlayButton() {
    this.playBtnStyle['display'] = 'block';
    if(this.artistName != ""){
      this.playBtnStyle['bottom'] = '10vh';
      this.pauseBtnStyle['bottom'] = '10vh';
    }
    else {
      this.playBtnStyle['bottom'] = '7vh';
      this.pauseBtnStyle['bottom'] = '7vh';
    }
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
