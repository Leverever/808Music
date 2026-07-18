import {AfterViewInit, Component, Input, OnDestroy, OnInit} from '@angular/core';
import {
  ArtistDetailResponse,
  ArtistGetByIdEndpointService
} from '../../../../endpoints/artist-endpoints/artist-get-by-id-endpoint.service';
import {ActivatedRoute, Router} from '@angular/router';
import {
  TrackGetAllEndpointService,
  TrackGetAllRequest
} from '../../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {
  TrackGetByIdEndpointService,
  TrackGetResponse
} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {
  AlbumGetAllEndpointService,
  AlbumGetAllResponse
} from '../../../../endpoints/album-endpoints/album-get-all-endpoint.service';
import {MyPagedList} from '../../../../services/auth-services/dto/my-paged-list';
import {MyConfig} from '../../../../my-config';
import {
  AlbumGetSpotlightEndpointService
} from '../../../../endpoints/album-endpoints/album-get-spotlight-endpoint.service';
import {Subscription} from 'rxjs';

@Component({
  selector: 'app-artist-music-page',
  templateUrl: './artist-music-page.component.html',
  styleUrl: './artist-music-page.component.css'
})
export class ArtistMusicPageComponent implements OnInit, AfterViewInit, OnDestroy{
  @Input() artist : ArtistDetailResponse | null = null;
  myPagedRequest : TrackGetAllRequest = {
    pageSize: 5,
    pageNumber: 1,
    isReleased: true,
    sortByStreams: true
  }
  myFeaturedRequest : TrackGetAllRequest =  {
    pageSize: 5,
    pageNumber: 1,
    isReleased: true,
    sortByStreams: true
  }
  tracks: TrackGetResponse[] = [];
  featuredTracks: TrackGetResponse[] = [];
  albums: MyPagedList<AlbumGetAllResponse> | null = null;
  featuredAlbums: MyPagedList<AlbumGetAllResponse> | null = null;
  featuredAlbum: AlbumGetAllResponse | null= null;

  isPlayingThisAlbum: boolean = false;
  playingState: boolean = false;

  state$! : Subscription;
  trackChange$! : Subscription;

  ngOnDestroy(): void {
    this.state$.unsubscribe();
    this.trackChange$.unsubscribe();
  }

  constructor(private router: Router,
              private trackGetAllService: TrackGetAllEndpointService,
              protected musicPlayerService: MusicPlayerService,
              private trackByIdService: TrackGetByIdEndpointService,
              private route: ActivatedRoute,
              private albumsGetService: AlbumGetAllEndpointService,
              private getArtistService : ArtistGetByIdEndpointService,
              private getSpotlightService : AlbumGetSpotlightEndpointService,) {
  }

  ngAfterViewInit(): void {
    /*
    this.myPagedRequest.leadArtistId = this.artist?.id;
    console.log(this.myPagedRequest);
    this.trackGetAllService.handleAsync(this.myPagedRequest).subscribe({
      next: data => {
        this.tracks = data.dataItems;
      }
    })

     */
    }

  ngOnInit(): void {
      this.route.params.subscribe(params => {
        let id = params['id'];
        if(id)
        {
          this.myPagedRequest.leadArtistId = id;
          this.myFeaturedRequest.featuredArtists = [id];
          this.trackGetAllService.handleAsync(this.myPagedRequest).subscribe({
            next: data => {
              this.tracks = data.dataItems;
            }
          })

          this.trackGetAllService.handleAsync(this.myFeaturedRequest).subscribe({
            next: data => {
              this.featuredTracks = data.dataItems;
            }
          })

          this.albumsGetService.handleAsync({artistId: id, isReleased: true, title: "", pageSize: 6, pageNumber: 1}).subscribe({
            next: data => {
              this.albums = data;
            }
          })

          this.getSpotlightService.handleAsync(id).subscribe({
            next: data => {
              this.featuredAlbum = data;
              this.isPlayingThisAlbum = this.musicPlayerService.getLastPlayedSong()?.albumId == this.featuredAlbum.id && this.musicPlayerService.getQueueType() === "album";
            }
          })

          this.albumsGetService.handleAsync({pageNumber: 1, pageSize: 6, isReleased:true, title:"", featuredArtistId: id}).subscribe({
            next: data => {
              this.featuredAlbums = data;
            }
          })

          this.getArtistService.handleAsync(id).subscribe({
            next: data => {
              this.artist = data
            }
          })
        }
      })
    this.playingState = this.musicPlayerService.getPlayState();

    this.state$ = this.musicPlayerService.playStateChange.subscribe(state => this.playingState = state);
    this.trackChange$ = this.musicPlayerService.trackEvent.subscribe(track =>
      this.isPlayingThisAlbum = track.albumId == this.featuredAlbum?.id && this.musicPlayerService.getQueueType() === "album");
  }

  createQueue(e: number) {
    console.log(this.tracks);
    this.musicPlayerService.createQueue(this.tracks, {display: `${this.artist?.name}'s Popular`, value: "/listener/profile/"+this.artist?.id}, "artist");
    let i = this.tracks.filter(val => val.id == e)[0];
    console.log(i);
    if(i)
    {
      this.musicPlayerService.skipTo(i);
    }
  }

  createFeaturedQueue(e: number) {
    this.musicPlayerService.createQueue(this.featuredTracks);
    let i = this.featuredTracks.filter(val => val.id == e)[0];
    if(i)
    {
      this.musicPlayerService.skipTo(i);
    }
  }

  getYear(releaseDate: string) {
    return new Date(releaseDate).getFullYear();
  }

  protected readonly MyConfig = MyConfig;

  goToAlbum(id: number) {
    this.router.navigate(["listener/release", id])
  }

  openSpotlight(): void {
    if(this.featuredAlbum)
    {
      this.goToAlbum(this.featuredAlbum.id);
    }
  }

  playFeatured() {
    this.trackGetAllService.handleAsync({isReleased:true, albumId: this.featuredAlbum?.id}).subscribe({
      next: data => {
        this.musicPlayerService.createQueue(data.dataItems,
          {display: this.featuredAlbum?.title + " - " + this.featuredAlbum?.type, value: "/listener/release/"+this.featuredAlbum?.id});
      }
    })
  }

  getTitle(title: string | undefined) {
    if(title == undefined)
    {
      return "";
    }
    let i = title.indexOf(" ");
    if(i <= -1 && title.length > 20 || i > 20 )
    {
      return title.substring(0, 17) + "...";
    }
    return title;
  }

  playAlbum(id: number, featured: boolean = false) {
    let a = undefined;

    if(featured)
    {
      a = this.featuredAlbums?.dataItems.find(item => item.id == id)
    }
    else
    {
      a = this.albums?.dataItems.find(item => item.id == id)
    }

    this.trackGetAllService.handleAsync({albumId: id}).subscribe({
      next: value => {
        this.musicPlayerService.createQueue(value.dataItems,{display:a?.title + " - " + a?.type, value:"/listener/release/"+id});
      }
    })
  }

  viewAll(featured: boolean) {
    if(!featured)
    {
      this.router.navigate(["listener/releases/"+this.artist?.id]);
    }
    else {
      this.router.navigate(["listener/releases/"+this.artist?.id], {queryParams: {featured: 'yes'}});
    }
  }
}
