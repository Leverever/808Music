import { Component, OnInit } from '@angular/core';
import { FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';
import { Observable } from 'rxjs';

import {
  PlaylistTracksGetEndpointService, PlaylistTracksGetRequest,
  PlaylistTracksGetResponse
} from '../../../../endpoints/playlist-endpoints/playlist-get-tracks-endpoint.service';
import { GetPlaylistByIdEndpointService, PlaylistByIdResponse } from '../../../../endpoints/playlist-endpoints/get-playlist-by-id-endpoint.service';
import { PlaylistUpdateTracksService, PlaylistUpdateTracksRequest } from '../../../../endpoints/playlist-endpoints/add-track-to-playlist-endpoint.service';
import { MyConfig } from '../../../../my-config';
import { HttpErrorResponse } from '@angular/common/http';
import { AlbumCoverService } from '../../../../endpoints/album-endpoints/album-get-cover-by-track-endpoint.service';
import { RemoveTrackFromPlaylistService } from '../../../../endpoints/playlist-endpoints/delete-track-from-playlist-endpoint.service';
import { DeletePlaylistService } from '../../../../endpoints/playlist-endpoints/playlist-delete-endpoint.service';
import { MyPagedList } from '../../../../services/auth-services/dto/my-paged-list';
import { GetPlaylistsByUserIdEndpointService } from '../../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import { MatDialog } from '@angular/material/dialog';
import { DeleteConfirmationDialogComponent } from '../../../shared/delete-confirmation-dialog/delete-confirmation-dialog.component';
import { MusicPlayerService } from '../../../../services/music-player.service';
import {
  TrackGetResponse,
  TrackUserInfoDto
} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {
  ShareBottomSheetComponent
} from '../../../shared/bottom-sheets/share-bottom-sheet/share-bottom-sheet.component';
import {PlaylistCreateDialogComponent} from './playlist-create-dialog/playlist-create-dialog.component';
import {MatBottomSheet} from '@angular/material/bottom-sheet';
import {Location} from '@angular/common';
import {MatSnackBar} from '@angular/material/snack-bar';
import {CollaboratorListDialogComponent} from './collaborator-list-dialog/collaborator-list-dialog.component';
import {
  PlaylistAddOrRemoveCollaboratorService
} from '../../../../endpoints/playlist-endpoints/add-or-remove-collaborator-endpoint.service';

@Component({
  selector: 'app-tracks-page',
  templateUrl: './tracks-list.component.html',
  styleUrls: ['./tracks-list.component.css'],
})
export class TracksPageComponent implements OnInit {
  playlist: PlaylistTracksGetResponse | null = null;
  playlistDetails: PlaylistByIdResponse | null = null;
  username: string = 'Loading...';
  userId: number = 0;
  isCollaborative = false;
  isOwner = false;
  playlistOwnerId = 0;
  coverPaths: { [trackId: number]: string } = {};
  showDeleteIcon: boolean = true;
  isLikedSongs = false;
  searchControl = new FormControl('');
  filteredTracks: TrackGetResponse[] = [];
  isUsersPlaylist = false;
  featuredTracks: TrackGetResponse[] = [];
  trackUserInfo: TrackUserInfoDto[] = [];
  myFeaturedRequest: { pageNumber: number; pageSize: number; PlaylistID : number } = {
    pageNumber: 1,
    pageSize: 1000,
    PlaylistID: this.playlist?.playlistId!
  };
  playlistUrl: string = '';

  constructor(
    private playlistTracksService: PlaylistTracksGetEndpointService,
    private playlistDetailsService: GetPlaylistByIdEndpointService,
    private playlistUpdateTracksService: PlaylistUpdateTracksService,
    private route: ActivatedRoute,
    private bottomSheet : MatBottomSheet,
    private getPlaylistsByUserIdService: GetPlaylistsByUserIdEndpointService,
    private router: Router,
    private albumCoverService: AlbumCoverService,
    private removeTrackFromPlaylistService: RemoveTrackFromPlaylistService,
    private deletePlaylistService: DeletePlaylistService,
    private dialog: MatDialog,
    private musicPlayerService: MusicPlayerService, private location : Location,
    private snackBar : MatSnackBar,
    private playlistAddOrRemoveCollaboratorService : PlaylistAddOrRemoveCollaboratorService

  ) {}

  ngOnInit(): void {
    this.userId = this.getUserIdFromToken();
    const playlistId = +this.route.snapshot.paramMap.get('id')!;
    this.playlistUrl = `${window.location.origin}/listener/playlist/${playlistId}`;

    if (playlistId) {
      this.loadPlaylistTracks(playlistId);
      this.loadPlaylistDetails(playlistId);
    }

    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((searchTerm) => this.searchTracks(searchTerm || ''))
      )
      .subscribe({
        next: (tracks) => {
          this.filteredTracks = tracks.dataItems || [];
        },
        error: (err) => {
          console.error('Error fetching tracks:', err);
          this.filteredTracks = [];
        },
      });

  }

  private loadPlaylistTracks(playlistId: number): void {
    const request = { playlistId, pageNumber: 1, pageSize: 50 };
    this.playlistTracksService.handleAsync(request).subscribe({
      next: (response) => {
        this.featuredTracks = response.dataItems || [];
        this.trackUserInfo = this.extractTrackUserInfo(response.dataItems);
        console.log(this.trackUserInfo);
        console.log(response);
      },
      error: (err: HttpErrorResponse) => {
        console.error('Error fetching playlist tracks:', err);
      },
    });
  }private extractTrackUserInfo(tracks: TrackGetResponse[]): any[] {
    return tracks.map(track => track.trackUserInfo).flat();
  }

  private loadPlaylistDetails(playlistId: number): void {
    this.playlistDetailsService.handleAsync(playlistId).subscribe({
      next: (response) => {
        this.playlistDetails = response;
        this.username = response.users[0].username;
        this.playlistOwnerId = response.users.find(user => user.isOwner)?.userId || 0; // Pronalaženje ID-a vlasnika
        this.isCollaborative = response.isCollaborative;
        this.isLikedSongs = response.isLikePlaylist;


        this.isOwner = this.playlistOwnerId === this.getUserIdFromToken();

        if (response.users[0].userId == this.getUserIdFromToken()) {
          this.isUsersPlaylist = true;
        }

        console.log(this.isUsersPlaylist);
      },
      error: (err: HttpErrorResponse) => {
        console.error('Error fetching playlist details:', err);
      },
    });
  }


  searchTracks(searchTerm: string): Observable<MyPagedList<TrackGetResponse>> {
    const request: PlaylistTracksGetRequest = { title: searchTerm, playlistId: this.playlistDetails?.id || 0, pageNumber: 1, pageSize: 10 };
    return this.playlistTracksService.handleAsync(request);
  }

  addTrackToPlaylist(trackId: number): void {
    if (!this.playlistDetails) return;

    const request: PlaylistUpdateTracksRequest = {
      userId : this.getUserIdFromToken(),
      playlistId: this.playlistDetails.id,
      trackIds: [trackId],
    };

    this.playlistUpdateTracksService.handleAsync(request).subscribe({
      next: () => {
        console.log(`Track with ID ${trackId} added to playlist`);
        if (this.playlistDetails?.id) {
          this.loadPlaylistTracks(this.playlistDetails.id);
        } else {
          console.error('Playlist details or ID is null');
        }
      },
      error: (err) => {
        console.error('Error adding track to playlist:', err);
      },
    });
  }

  removeTrack(trackId: number): void {
    const playlistId = this.playlistDetails?.id;
    if (playlistId) {
      this.removeTrackFromPlaylistService.handleAsync(playlistId, trackId).subscribe({
        next: () => {
          console.log(`Track ${trackId} successfully removed from playlist ${playlistId}`);
          this.loadPlaylistTracks(playlistId);
        },
        error: (error) => {
          console.error('Error removing track:', error);
        },
      });
    }
  }

  goBack(): void {
  this.location.back();
  }
  generateInviteLink(): string {
    if (!this.playlistDetails) return '';

    const playlistId = this.playlistDetails.id;
    const inviteToken = Math.random().toString(36).substr(2, 25);
    const ownerId = this.getUserIdFromToken();
    return `${window.location.origin}/listener/playlist/collaborate?p=${playlistId}&${inviteToken}&oiwd=${ownerId}`;
  }
  copyInviteLink(): void {
    if (this.playlistDetails && this.playlistDetails.users.length === 5) {
      this.snackBar.open('Collab capacity is five users per playlist', 'Close', {
        duration: 3000,
        verticalPosition: 'bottom',
        horizontalPosition: 'center',
      });
    } else {
      const link = this.generateInviteLink();
      navigator.clipboard.writeText(link).then(() => {
        console.log('Invite link copied to clipboard:', link);
        this.snackBar.open('Link copied to Clipboard!', 'Close', {
          duration: 1500,
          verticalPosition: 'bottom',
          horizontalPosition: 'center',
        });
      }).catch(err => {
        console.error('Failed to copy invite link:', err);
      });
    }
  }


  getTotalTrackLength(): string {
    const totalSeconds = this.featuredTracks?.reduce((total, track) => total + track.length, 0) || 0;
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}m ${seconds}s`;
  }

  deletePlaylist(): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent);

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        if (this.playlistDetails?.id) {
          this.deletePlaylistService.handleAsync(this.playlistDetails.id).subscribe({
            next: () => {
              this.router.navigate(['/listener/playlist']);
            },
            error: (error) => {
              console.error('Error deleting playlist:', error);
            },
          });
        }
      } else {
        console.log('Playlist deletion canceled');
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

  protected readonly MyConfig = MyConfig;

  sharePlaylist() {
    this.bottomSheet.open(ShareBottomSheetComponent, {
      data: { url: this.playlistUrl },
      hasBackdrop: true,
      panelClass: ['liquid-glass-sheet-pane', 'listener-content-sheet-pane'],
      backdropClass: 'liquid-glass-sheet-backdrop',
    });
  }

  createFeaturedQueue(e: number) {
    this.musicPlayerService.createQueue(this.featuredTracks);
    let i = this.featuredTracks.filter(val => val.id == e)[0];
    if (i) {
      this.musicPlayerService.skipTo(i);
    }
  }

  editPlaylist(): void {
    if (!this.playlistDetails) {
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
      data: {
        playlistDetails: this.playlistDetails,
      },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadPlaylistDetails(this.playlistDetails?.id!);
      }
    });
  }

  openProfile(userId: number) {
    this.router.navigate([`/listener/user/`, userId]);
  }

  filterSearch($event: string) {
    this.loadPlaylistTracks(this.playlist?.playlistId!);
  }

  protected readonly open = open;

  get playlistArtworkCss(): string {
    if(!this.playlistDetails?.coverPath)
    {
      return 'none';
    }

    return `url("${MyConfig.media_address}${this.playlistDetails.coverPath}")`;
  }

  manageCollaborators() {
    if (!this.playlistDetails) return;

    const dialogRef = this.dialog.open(CollaboratorListDialogComponent, {
      width: '500px',
      data: {
        collaborators: this.playlistDetails.users,
      },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        const request = {
          ownerId : this.getUserIdFromToken(),
          collaboratorId : result.userId,
          playlistId : this.playlistDetails!.id,
        }

        this.playlistAddOrRemoveCollaboratorService.handleAsync(request).subscribe(result => {
          this.loadPlaylistDetails(this.playlistDetails?.id!);
          this.snackBar.open('User removed as Collaborator successfully!', 'Close', {
            duration: 1500,
            verticalPosition: 'bottom',
            horizontalPosition: 'center'
          });
        });
        console.log('Collaborators dialog closed with result:', result);
      }
    });
  }

  startPlaylist($event: void) {
    this.musicPlayerService.createQueue(this.featuredTracks, {display: this.playlist?.playlistTitle ?? "Playlist", value: "/listener/playlist"+this.playlist?.playlistId}, "playlist");
  }
}
