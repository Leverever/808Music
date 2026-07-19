import {Component, OnInit, inject} from '@angular/core';
import { DeletePlaylistService } from '../../../../endpoints/playlist-endpoints/playlist-delete-endpoint.service';
import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ConfirmDialogComponent } from '../../../shared/dialogs/confirm-dialog/confirm-dialog.component';
import { MyConfig } from '../../../../my-config';
import { GetPlaylistsByUserIdEndpointService } from '../../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import { PlaylistUpdateEndpointService } from '../../../../endpoints/playlist-endpoints/update-playlist-endpoint.service';
import { HttpErrorResponse } from '@angular/common/http';
import { PlaylistResponse } from '../../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {PlaylistCreateDialogComponent} from '../tracks-page/playlist-create-dialog/playlist-create-dialog.component';
import {
  PlaylistTracksGetEndpointService
} from '../../../../endpoints/playlist-endpoints/playlist-get-tracks-endpoint.service';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {
  PersonalizedPlaylistSummary,
  PersonalizedPlaylistsEndpointService
} from '../../../../endpoints/personalization-endpoints/personalized-playlists-endpoint.service';
import {RecommendationTrackMapper} from '../../../../services/personalization/recommendation-track.mapper';

@Component({
  selector: 'app-playlist-list-material',
  templateUrl: './playlist-list-material.component.html',
  styleUrls: [
   './playlist-list-material.component.css'
  ]
})
export class PlaylistListMaterialComponent implements OnInit {
  playlists: PlaylistResponse[] | null = null;
  dialog = inject(MatDialog);
  snackBar = inject(MatSnackBar);
  userId: number | null = null;
  dailyPlaylists: PersonalizedPlaylistSummary[] = [];
  isListenerMode = false;

  constructor(
    private playlistService: GetPlaylistsByUserIdEndpointService,
    private playlistDeleteService: DeletePlaylistService,
    private router: Router,
    private playlistUpdateService: PlaylistUpdateEndpointService,
    private tracksService: PlaylistTracksGetEndpointService,
    private musicPlayerService: MusicPlayerService,
    private personalizedPlaylistsEndpoint: PersonalizedPlaylistsEndpointService,
    private recommendationTrackMapper: RecommendationTrackMapper,
  ) {}

  ngOnInit(): void {
    this.isListenerMode = this.router.url.startsWith('/listener');
    this.userId = this.getUserIdFromToken();
    this.loadPlaylists();
    this.loadDailyPlaylists();
    console.log(this.userId);
  }

  loadDailyPlaylists() {
    this.personalizedPlaylistsEndpoint.getDaily().subscribe({
      next: response => this.dailyPlaylists = response.playlists,
      error: error => console.warn('Could not load daily personalized playlists.', error)
    });
  }

  openDailyPlaylist(id: string) {
    this.router.navigate(['/listener/playlist/daily', id]);
  }

  mediaUrl(path?: string): string {
    const value = path || '/media/Images/ArtistPfps/placeholder.png';
    if(/^https?:\/\//i.test(value))
    {
      return value;
    }

    const normalizedPath = value.startsWith('/media/')
      ? value
      : `/media/${value.replace(/^\/+/, '')}`;
    return `${MyConfig.api_address}${normalizedPath}`;
  }

  playlistCoverUrl(path?: string): string {
    const value = path || '/media/Images/playlist_placeholder.png';
    if(/^https?:\/\//i.test(value))
    {
      return value;
    }

    if(value.startsWith('/media/'))
    {
      return `${MyConfig.api_address}${value}`;
    }

    return `${MyConfig.media_address}${value.replace(/^\/+/, '')}`;
  }

  startDailyPlaylist(id: string) {
    this.personalizedPlaylistsEndpoint.getById(id).subscribe({
      next: playlist => {
        const tracks = this.recommendationTrackMapper.toPlayerTracks(playlist.tracks);
        if(tracks.length === 0)
        {
          this.snackBar.open('This daily playlist has no songs yet.', '', {duration: 2000});
          return;
        }

        this.musicPlayerService.createQueue(
          tracks,
          {display: `${playlist.name} - Daily Mix`, value: `/listener/playlist/daily/${playlist.id}`},
          'personalized-playlist');
      },
      error: error => console.error('Could not start daily playlist.', error)
    });
  }

  loadPlaylists() {
    if (this.userId) {
      this.playlistService.handleAsync(this.userId).subscribe(playlists => {
        console.log('Playlists loaded:', playlists);
        this.playlists = playlists || [];
      });
    }
  }

  deletePlaylist(id: number) {
    let playlist = this.playlists?.find(x => x.id === id);
    let matRef = this.dialog.open(ConfirmDialogComponent, {
      hasBackdrop: true,
      data: {
        title: `Are you sure you want to delete "${playlist?.title}"?`,
        content: 'This will delete every track in the playlist.'
      }
    });

    matRef.afterClosed().subscribe(res => {
      if (res) {
        this.playlistDeleteService.handleAsync(id).subscribe({
          error: () => {
            alert('Playlist deletion failed.');
          },
          complete: () => {
            this.snackBar.open(`"${playlist?.title}" deleted successfully.`, 'Dismiss', { duration: 3000 });
            this.loadPlaylists();
          }
        });
      }
    });
  }

  openPlaylist(playlistId: number) {
    this.router.navigate([`/listener/playlist/${playlistId}`]);
  }

  editPlaylist(id: number) {
    const playlist = this.playlists?.find(item => item.id === id);
    if (!playlist) {
      return;
    }

    const dialogRef = this.dialog.open(PlaylistCreateDialogComponent, {
      width: 'min(680px, calc(100vw - 24px))',
      maxWidth: '680px',
      maxHeight: 'calc(100dvh - 24px)',
      panelClass: 'playlist-create-dialog-pane',
      backdropClass: 'playlist-create-dialog-backdrop',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      data: {playlistDetails: playlist},
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadPlaylists();
      }
    });
  }
  createPlaylist() {
    const dialogRef = this.dialog.open(PlaylistCreateDialogComponent, {
      width: 'min(680px, calc(100vw - 24px))',
      maxWidth: '680px',
      maxHeight: 'calc(100dvh - 24px)',
      panelClass: 'playlist-create-dialog-pane',
      backdropClass: 'playlist-create-dialog-backdrop',
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      data: {},
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        console.log('Playlist successfully created:', result);
        this.loadPlaylists();
      }
    });
  }
  getReleaseYear(releaseDate: string) {
    return new Date(releaseDate).getFullYear().toString();
  }

  protected readonly MyConfig = MyConfig;

  updatePlaylist(id: number) {
    let formData = new FormData();
    formData.append('title', 'Updated Playlist Title');
    formData.append('isPublic', 'true');

    this.playlistUpdateService.handleAsync(id, formData).subscribe({
      next: () => {
        this.snackBar.open('Playlist updated successfully.', 'Dismiss', { duration: 3000 });
        this.loadPlaylists();
      },
      error: (err: HttpErrorResponse) => {
        console.error('Error updating playlist:', err);
        this.snackBar.open('Error updating playlist.', 'Dismiss', { duration: 3000 });
      }
    });
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

  startPlaylist(id: number, playlist: PlaylistResponse) {
    this.tracksService.handleAsync({playlistId: id, pageSize:100000, pageNumber:1}).subscribe({
      next: value => {
        if(value.dataItems.length == 0)
        {
          this.snackBar.open('Playlist has no songs', "", {duration: 2000});
          return;
        }
        this.musicPlayerService.createQueue(value.dataItems, {display: playlist.title + " - Playlist", value: "/listener/playlist/" + playlist.id}, "playlist");
      }
    })
  }
}
