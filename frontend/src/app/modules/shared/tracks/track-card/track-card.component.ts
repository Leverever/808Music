import {AfterViewInit, Component, EventEmitter, Input, OnDestroy, OnInit, Output} from '@angular/core';
import {ArtistTrackDto, TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {ArtistSimpleDto} from '../../../../services/auth-services/dto/artist-dto';
import {Router} from '@angular/router';
import {MyConfig} from '../../../../my-config';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {Subscription} from 'rxjs';

@Component({
  selector: 'app-track-card',
  templateUrl: './track-card.component.html',
  styleUrl: './track-card.component.css'
})
export class TrackCardComponent implements AfterViewInit, OnInit, OnDestroy {
  @Input() track : TrackGetResponse | null = null;
  @Input() artistMode : boolean = false;
  @Input() useCustomPlayHandler: boolean = false;
  @Output() playRequested = new EventEmitter<void>();

  playBtnSyle = {
    'display': 'none',
  };
  pauseBtnStyle = {
    'display': 'block'
  }

  cardStyle = {
    width: '32vw'
  };

  isPlayingThisTrack: boolean = false;
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
    this.isPlayingThisTrack = this.musicPlayerService.getLastPlayedSong()?.id == this.track?.id;
    this.playingState = this.musicPlayerService.getPlayState();

    this.state$ = this.musicPlayerService.playStateChange.subscribe(state => this.playingState = state);
    this.trackChange$ = this.musicPlayerService.trackEvent.subscribe(track => this.isPlayingThisTrack = track.id == this.track?.id);
  }

  showPlayButton(b: boolean) {
    if(b)
    {
      this.playBtnSyle['display'] = 'block';
    }
    else {
      this.playBtnSyle['display'] = 'none';
    }
  }

  ngAfterViewInit(): void {
    if(this.artistMode)
    {
      this.cardStyle['width'] = '20vw';
    }
  }

  goToArtistPage(artist: ArtistSimpleDto | ArtistTrackDto) {
    this.router.navigate(["/listener/profile", artist.id]);
  }

  protected readonly MyConfig = MyConfig;


  goToTrack() {
    if(this.artistMode){
      this.router.navigate([`/artist/tracks/${this.track?.albumId}/edit/${this.track?.id}?albumId=${this.track?.albumId}`]);
      console.log("HERE");
    }
    else
    {
      this.router.navigate(["/listener/release", this.track?.albumId]);
    }
  }

  handleActionClick() {
    if(this.artistMode)
    {
      this.router.navigate([`/artist/tracks/${this.track?.albumId}/edit/${this.track?.id}?albumId=${this.track?.albumId}`]);
    }
    else {
      if(this.useCustomPlayHandler)
      {
        this.playRequested.emit();
        return;
      }

      this.musicPlayerService.createQueue([this.track!], {display: this.track!.title, value: "/listener/release/"+this.track!.albumId});
    }
  }

  getCoverUrl(path: string): string {
    return /^https?:\/\//i.test(path) ? path : MyConfig.api_address + path;
  }
}
