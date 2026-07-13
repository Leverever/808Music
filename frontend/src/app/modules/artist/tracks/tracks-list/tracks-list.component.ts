import {Component, inject, Input, OnInit} from '@angular/core';
import {ActivatedRoute, NavigationStart, Router} from '@angular/router';
import {
  AlbumGetByIdEndpointService,
  AlbumGetResponse
} from '../../../../endpoints/album-endpoints/album-get-by-id-endpoint.service';
import {MyConfig} from '../../../../my-config';
import {ArtistSimpleDto} from '../../../../services/auth-services/dto/artist-dto';
import {ArtistHandlerService} from '../../../../services/artist-handler.service';
import {
  TrackGetAllEndpointService,
  TrackGetAllRequest
} from '../../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {MyPagedList} from '../../../../services/auth-services/dto/my-paged-list';
import {Location, LocationStrategy, PathLocationStrategy} from '@angular/common';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {MatDialog} from '@angular/material/dialog';
import {
  PlaylistUpdateTracksService
} from '../../../../endpoints/playlist-endpoints/add-track-to-playlist-endpoint.service';
import {
  GetPlaylistsByUserIdEndpointService, PlaylistResponse
} from '../../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {PlaylistDialogComponent} from '../../../shared/playlist-detail/playlist-dialog.component';
import moment from 'moment';

@Component({
  selector: 'app-tracks-page',
  templateUrl: './tracks-list.component.html',
  styleUrl: './tracks-list.component.css',
  providers: [Location, {provide: LocationStrategy, useClass: PathLocationStrategy}]
})
export class TracksListComponent implements OnInit {
  album : AlbumGetResponse | null = null;
  artist : ArtistSimpleDto | null = null;
  tracks: TrackGetResponse[] = [];
  playlists: PlaylistResponse[] = [];
  currentUserId: number | null = null;
  pagedResponse: MyPagedList<TrackGetResponse> | null = null;
  pagedRequest : TrackGetAllRequest = {
    pageNumber:1,
    pageSize:20
  }
  location = inject(Location)
  selectedTrack: TrackGetResponse | null = null;
  protected readonly MyConfig = MyConfig;
  @Input() isHome: boolean = false;
  private albumId = -1;
  reloadTable = false;
  @Input() artistMode: boolean = true;

  constructor(private router: Router,
              private route : ActivatedRoute,
              private albumGetService : AlbumGetByIdEndpointService,
              private artistHandler : ArtistHandlerService,
              private tracksGetAllService : TrackGetAllEndpointService,
              private musicPlayerService: MusicPlayerService,
              private dialog: MatDialog,
              private playlistService: GetPlaylistsByUserIdEndpointService,
              private playlistUpdateService: PlaylistUpdateTracksService,) {
  }

  ngOnInit(): void {
    if (this.artistMode) {
      this.homeCheck(this.router.url);
    }
    this.checkIfHome();
    this.reloadData();
    //this.getCurrentUserId();
    //this.loadPlaylists();
  }

  reloadData() {
    this.route.params.subscribe(params => {
      let id: number = params["id"];
      this.pagedRequest.albumId = id;

      this.albumGetService.handleAsync(id).subscribe({
        next: data => {
          this.album = data;
          this.artist = {
            id: data.artist.id,
            isFlaggedForDeletion: false,
            role: "",
            deletionDate:"",
            name: data.artist.name,
            pfpPath: "/media/Images/ArtistPfps/" + data.artist.profilePhotoPath
          };
        }
      });

      this.tracksGetAllService.handleAsync(this.pagedRequest).subscribe({
        next: data => {
          this.tracks = data.dataItems;
          this.pagedResponse = data;
          console.log(data);
        }
      })
    })
  }

  getYear() {
    return new Date(this.album?.releaseDate!).getFullYear();
  }

  getCurrentUserId(): void {
    const authToken = sessionStorage.getItem('authToken');
    if (!authToken) {
      throw new Error('User not authenticated.');
    }

    try {
      const parsedToken = JSON.parse(authToken);
      if (!parsedToken.userId) {
        throw new Error('Invalid token: userId not found.');
      }
      console.log(parsedToken.userId)
      return parsedToken.userId;
    } catch (error) {
      throw new Error('Failed to parse authToken.');
    }
  }

  loadPlaylists(): void {
    if (this.currentUserId) {
      this.playlistService.handleAsync(this.currentUserId).subscribe({
        next: data => {
          this.playlists = data;
        },
        error: () => {
          console.error('Error loading playlists');
        }
      });
    }
  }

  openTrackMenu(track: TrackGetResponse): void {
    if (this.playlists.length > 0) {
      const dialogRef = this.dialog.open(PlaylistDialogComponent, {
        width: '400px',
        data: { track, playlists: this.playlists }
      });

      dialogRef.afterClosed().subscribe(result => {
        if (result) {
          this.addTrackToPlaylist(result.playlistId, track);
        }
      });
    } else {
      alert('No playlists found');
    }
  }
  private getUserIdFromToken(): number {
    let authToken = sessionStorage.getItem('authToken');

    if (!authToken) {
      authToken = localStorage.getItem('authToken');
    }

    if (!authToken) {
      return 0;
    }

    try {
      const parsedToken = JSON.parse(authToken);
      return parsedToken.userId;
    } catch (error) {
      console.error('Error parsing authToken:', error);
      return 0;
    }
  }
  addTrackToPlaylist(playlistId: number, track: TrackGetResponse): void {
    if (this.currentUserId) {
      const request = {
        playlistId,
        trackIds: [track.id],
        userId : this.getUserIdFromToken()
      };

      this.playlistUpdateService.handleAsync(request).subscribe({
        next: () => {
          console.log('Track added to playlist');
        },
        error: () => {
          console.error('Error adding track to playlist');
        }
      });
    }}
  getAlbumDuration(lengthInSeconds: number | undefined) {
    if(lengthInSeconds != undefined)
    {
      let hours = Math.floor(lengthInSeconds / 3600).toString();
      let minutes = Math.floor((lengthInSeconds % 3600)/60).toString();
      let seconds = Math.floor((lengthInSeconds % 3600)%60).toString();
      return `${hours}h ${minutes}m ${seconds}s`;
    }
    return "";
  }

  isAlbumExplicit() : boolean{
    for (const track of this.tracks) {
      if(track.isExplicit)
      {
        return true;
      }
    }
    return false;
  }

  goBack(e: MouseEvent) {
    if(this.artistMode)
    {
      this.router.navigate(['/artist/album']);
    }
    else {
      this.location.back();
    }
  }

  logData(track: TrackGetResponse) {
    console.log(track);
  }

  editTrack(e: number) {
    if(this.artistMode)
    {
      this.router.navigate(["/artist/tracks", e]);
    }
    else {
      this.tracksGetAllService.handleAsync({pageNumber: 1, pageSize: 100000, albumId: this.album!.id}).subscribe({
        next: data => {
          this.musicPlayerService.createQueue(data.dataItems,{display:this.album?.title + " - " + this.album?.type.type, value: "./listener/release/"+this.album?.id})
          this.musicPlayerService.skipTo(data.dataItems.find(x=> x.id == e)!);
          console.log(this.tracks.find(x=> x.id == e)!)
        }
      });
    }

    //this.router.navigate(["/artist/tracks/"+this.album!.id+"/edit/"+e.id], { queryParams: {albumId: this.album!.id}});
  }

  addNew() {
    if(this.artistMode)
    {
      this.router.navigate(["/artist/tracks/create"]);
    }
    else {
      this.tracksGetAllService.handleAsync({pageNumber:1, pageSize:100000, albumId: this.album?.id}).subscribe({
        next: data => {
          this.musicPlayerService.createQueue(data.dataItems, {display:this.album?.title + " - " + this.album?.type.type, value: "./listener/release/"+this.album?.id})
        }
      })
    }
  }

  checkIfHome() {
    this.router.events.subscribe(event => {
      if(event instanceof NavigationStart)
      {
        this.homeCheck(event.url);
      }
    })
  }

  homeCheck(url: string) {
    this.route.params.subscribe(params => {
      let id = params["id"];
      if(url == "/artist/album/"+id+"/tracks")
      {
        this.reloadData();
        this.reloadTable = true;
        this.isHome = true;
      }
      else {
        this.isHome = false;
        this.reloadTable = false;
      }
    })
  }

  protected readonly moment = moment;
}
