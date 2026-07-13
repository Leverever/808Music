import {Component, inject, OnDestroy, OnInit} from '@angular/core';
import {
  TrackGetByIdEndpointService,
  TrackGetResponse
} from '../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {MyConfig} from '../../../my-config';
import {AlbumGetByIdEndpointService} from '../../../endpoints/album-endpoints/album-get-by-id-endpoint.service';
import {
  TrackGetAllEndpointService,
  TrackGetAllRequest
} from '../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {ProductAddResponse} from '../../../endpoints/products-endpoints/product-create-endpoint.service';
import {MyPagedList} from '../../../services/auth-services/dto/my-paged-list';
import {MusicPlayerService} from '../../../services/music-player.service';
import {MatBottomSheet} from '@angular/material/bottom-sheet';
import {
  QueueViewBottomSheetComponent
} from '../../shared/bottom-sheets/queue-view-bottom-sheet/queue-view-bottom-sheet.component';
import {ShareBottomSheetComponent} from '../../shared/bottom-sheets/share-bottom-sheet/share-bottom-sheet.component';
import {queue} from 'rxjs';
import {Router} from '@angular/router';
import {
  IsSubscribedRequest,
  IsSubscribedService
} from '../../../endpoints/auth-endpoints/is-subscribed-endpoint.service';
import {MatDialog} from '@angular/material/dialog';
import {PleaseSubscribeComponent} from '../../shared/bottom-sheets/please-subscribe/please-subscribe.component';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';
import {SendSongMessageComponent} from '../../shared/bottom-sheets/send-song-message/send-song-message.component';
import {
  PlaylistUpdateTracksRequest, PlaylistUpdateTracksService
} from '../../../endpoints/playlist-endpoints/add-track-to-playlist-endpoint.service';
import {MatSnackBar} from '@angular/material/snack-bar';
import {PlaylistResponse} from '../../../endpoints/playlist-endpoints/get-all-playlists-endpoint.service';
import {
  GetPlaylistsByUserIdEndpointService
} from '../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {AddTrackToLikedSongsService} from '../../../endpoints/playlist-endpoints/add-to-liked-songs-endpoint';
import {IsLikedSongService} from '../../../endpoints/playlist-endpoints/is-liked-song-endpoint.service';
import {
  RemoveTrackFromPlaylistService
} from '../../../endpoints/playlist-endpoints/delete-track-from-playlist-endpoint.service';
import {IsOnPlaylistService} from '../../../endpoints/playlist-endpoints/is-song-on-playlist-endpoint.service';
import {TrackInteractionService} from '../../../services/personalization/track-interaction.service';

@Component({
  selector: 'app-music-player',
  templateUrl: './music-player.component.html',
  styleUrl: './music-player.component.css',
  standalone: false
})
export class MusicPlayerComponent implements OnInit, OnDestroy {
  track:TrackGetResponse | null = null;
  playlists: PlaylistResponse[] = [];
  trackId = 0;
  newTrackId: number = 39;
  queueManager = inject(MatBottomSheet)
  isSubbed = true;
  private showPlaylistDropdown: boolean = false;
  selectedTrackId  = 0;
  likedSongs: Map<number, boolean> = new Map();
  playlistTrackMap: Map<number, Map<number, boolean>> = new Map();

  constructor(private trackGetService: TrackGetByIdEndpointService,
              private albumGetService: TrackGetAllEndpointService, private removeTrackFromPlaylistService : RemoveTrackFromPlaylistService,
              private albumByIdService: AlbumGetByIdEndpointService,
              protected musicPlayerService: MusicPlayerService, private isOnPlaylist : IsOnPlaylistService,
              private router: Router, private addTrackToLikedSongsService : AddTrackToLikedSongsService,
              private isSubscribedService : IsSubscribedService, private getPlaylistsService: GetPlaylistsByUserIdEndpointService,
              private dialog : MatDialog,private snackBar : MatSnackBar, private isLikedSongService : IsLikedSongService,
              private auth: MyUserAuthService, private playlistUpdateTracksService : PlaylistUpdateTracksService,
              private interactions: TrackInteractionService) {
  }

  ngOnDestroy(): void {
        this.musicPlayerService.queue = [];
  }

  ngOnInit(): void {
    this.loadPlaylists();

    /*
    this.albumGetService.handleAsync({albumId: this.newTrackId}).subscribe({
      next: (response: MyPagedList<TrackGetResponse>) => {
        this.albumByIdService.handleAsync(this.newTrackId).subscribe({
          next: value => {
            this.musicPlayerService.createQueue(response.dataItems, {display:value.title+ " - " + value.type.type, value:"/artist/album/edit/"+value.id});
          }
        })
      }
    })
     */

    const request: IsSubscribedRequest = {
      userId : this.auth.getAuthToken()!.userId
    };
    this.isSubscribedService.handleAsync(request).subscribe({
      next: (response) => {
        if (!response.isSubscribed)
        {
          this.openPleaseSubscribeDialog();
          this.isSubbed = false;
        }
        else {
          this.isSubbed = true;
        }
      },
      error: (err) => {
        console.error('Error:', err);
      },
    });

    this.track = this.musicPlayerService.getLastPlayedSong();
    this.trackId = this.musicPlayerService.getLastPlayedSong()!.id;
    this.isLikedLoad();

    this.musicPlayerService.trackEvent.subscribe({
      next: data => {
        this.track = data;
        this.trackId = this.track.id;
        this.isLikedLoad();

      }
    })
  }

  protected readonly MyConfig = MyConfig;

  setNewSong() {
    this.trackGetService.handleAsync(this.newTrackId).subscribe({
      next: data => {
        this.musicPlayerService.addToQueue(data);
        this.trackId = data.id;
        console.log(this.musicPlayerService.queue);
      }
    })
  }

  openQueueManager() {
    let queue = this.musicPlayerService.getQueue();
    this.queueManager.open(QueueViewBottomSheetComponent, {data: {queue}});
  }

  openShareSheet() {
    this.queueManager.open(ShareBottomSheetComponent, {data: {url: MyConfig.ui_address + "/listener/track/"+ this.track?.id}});
  }
  isLikedLoad() {
    const requestLike = { trackId: this.trackId, userId: this.getUserIdFromToken() };
    this.isLikedSongService.handleAsync(requestLike).subscribe({
      next: response => {
        console.log("This song is", response);
        this.likedSongs.set(this.trackId, response.isLikedSong);
      },
    });
  }
  redirectToSource() {
    this.router.navigate([this.musicPlayerService.queueSource.value]);
  }

  goToRelease() {
    this.router.navigate(["/listener/release", this.track!.albumId]);
  }

  goHome() {
    this.router.navigate(["/listener/home"]);
  }

  goToSearch() {
    this.router.navigate(["/listener/search"]);
  }

  messageBottomSheet() {
    /*
    this.musicPlayerService.setAutoPlayStatus(!this.musicPlayerService.getAutoPlayStatus());
    console.log(this.musicPlayerService.getAutoPlayStatus());
     */
    let ref = this.queueManager.open(SendSongMessageComponent, {data: {track: this.track}});
  }

  private openPleaseSubscribeDialog(): void {
    const dialogRef = this.dialog.open(PleaseSubscribeComponent, {
      width: '400px',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result: string | undefined ) => {
      if (result === 'navigate') {
        this.router.navigate(['listener/subscriptions']);
      }
    });
  }

  playlistDropdown(id: number | undefined) {
      this.selectedTrackId = id!;
    this.initializePlaylistCheckboxes(id!);

    this.showPlaylistDropdown = !this.showPlaylistDropdown;
      console.log(this.showPlaylistDropdown);

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
  loadPlaylists() {
    const userId = this.getUserIdFromToken();
    if (userId) {
      this.getPlaylistsService.handleAsync(userId).subscribe({
        next: (playlists) => {
          this.playlists = playlists;
          console.log(this.playlists);

        },
        error: (error) => {
          console.error('Error loading playlists:', error);
        },
      });
    }
  }
  addToPlaylist(playlistId: number) {
    if (this.selectedTrackId) {
      const isInPlaylist = this.isTrackInPlaylist(this.selectedTrackId, playlistId);

      if (isInPlaylist) {
        this.removeTrackFromPlaylistService.handleAsync(playlistId, this.selectedTrackId).subscribe({
          next: () => {
            this.snackBar.open('Track removed from playlist!', 'Dismiss', { duration: 3500 });
            this.playlistTrackMap.get(playlistId)?.set(this.selectedTrackId!, false);
            this.showPlaylistDropdown = false;
          },
          error: (error) => {
            console.error('Error removing track from playlist:', error);
          },
        });
      } else {
        const request: PlaylistUpdateTracksRequest = {
          playlistId: playlistId,
          userId : this.getUserIdFromToken(),
          trackIds: [this.selectedTrackId],
        };

        this.playlistUpdateTracksService.handleAsync(request).subscribe({
          next: () => {
            this.snackBar.open('Track added to playlist!', 'Dismiss', { duration: 3500 });
            this.showPlaylistDropdown = false;
          },
          error: (error) => {
            console.error('Error adding track to playlist:', error);
          },
        });
      }
    }
  }

  getLikeIcon(id: number | undefined) {
    return this.likedSongs.get(id!) ? 'favorite' : 'favorite_border';
  }

  addToLikedSongs(id: number | undefined) {
    const isLiked = this.likedSongs.get(id!) || false;
    const request = { trackId: id!, userId: this.getUserIdFromToken() };

    if (isLiked) {
      this.addTrackToLikedSongsService.handleAsync(request).subscribe({
        next: () => {
          this.likedSongs.set(id!, false);
          this.interactions.record(id!, 'Unliked', {contextType: this.getInteractionContext()});
          this.snackBar.open("Song removed from liked songs", "Dismiss", { duration: 3500 });
        },
        error: error => {
          console.error('Error removing track:', error);
        },
      });
    } else {
      this.addTrackToLikedSongsService.handleAsync(request).subscribe({
        next: () => {
          this.likedSongs.set(id!, true);
          this.interactions.record(id!, 'Liked', {contextType: this.getInteractionContext()});
          this.snackBar.open("Song added to liked songs", "Dismiss", { duration: 3500 });
        },
        error: error => {
          console.error('Error adding track:', error);
        },
      });
    }
  }

  goToTrackRadio(): void {
    if(this.track == null)
    {
      return;
    }

    this.router.navigate(['/listener/radio', this.track.id]);
  }

  private getInteractionContext() {
    switch(this.musicPlayerService.getQueueType())
    {
      case 'autoplay': return 'Autoplay' as const;
      case 'radio': return 'Radio' as const;
      case 'playlist':
      case 'personalized-playlist': return 'Playlist' as const;
      case 'song': return 'Manual' as const;
      default: return 'Playback' as const;
    }
  }
  initializePlaylistCheckboxes(trackId: number): void {
    if (!this.playlists.length) return;

    this.playlists.forEach(playlist => {
      const request = { playlistId: playlist.id, trackId: trackId };
      console.log("checkbox", request);
      this.isOnPlaylist.handleAsync(request).subscribe({
        next: (response) => {
          if (!this.playlistTrackMap.has(playlist.id)) {
            this.playlistTrackMap.set(playlist.id, new Map());
          }
          this.playlistTrackMap.get(playlist.id)!.set(trackId, response.isAlreadyOnPlaylist);
        },
        error: (error) => {
          console.error(`Error checking track ${trackId} on playlist ${playlist.id}:`, error);
        },
      });
    });
  }
  isTrackInPlaylist(trackId : number, playlistId: number) {
    return this.playlistTrackMap.get(playlistId)?.get(trackId) ?? false;
  }
}
