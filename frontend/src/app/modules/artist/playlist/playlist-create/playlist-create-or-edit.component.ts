import { Component, inject, Input, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  PlaylistAddRequest,
  PlaylistCreateService
} from '../../../../endpoints/playlist-endpoints/create-playlist-endpoint.service';
import { PlaylistUpdateEndpointService } from '../../../../endpoints/playlist-endpoints/update-playlist-endpoint.service';
import { MyConfig } from '../../../../my-config';
import { HttpErrorResponse } from '@angular/common/http';
import { GetPlaylistByIdEndpointService } from '../../../../endpoints/playlist-endpoints/get-playlist-by-id-endpoint.service';

@Component({
  selector: 'app-playlist-create-or-edit',
  templateUrl: './playlist-create-or-edit.component.html',
  styleUrls: ['./playlist-create-or-edit.component.css']
})
export class PlaylistCreateOrEditComponent implements OnInit {
  @Input() playlist: any = {
    title: '',
    isPublic: false,
    coverImage: undefined,
    trackIds: []
  };

  existingCoverPath: string = '';
  snackBar = inject(MatSnackBar);

  constructor(
    private router: Router,
    private route: ActivatedRoute,
    private playlistCreateService: PlaylistCreateService,
    private playlistUpdateService: PlaylistUpdateEndpointService,
    private playlistGetByIdService: GetPlaylistByIdEndpointService
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      if (params['id']) {
        this.loadPlaylist(params['id']);
      }
    });
  }

  loadPlaylist(id: number) {
    this.playlistGetByIdService.handleAsync(id).subscribe({
      next: (playlist) => {
        this.playlist = playlist;
        this.existingCoverPath = playlist.coverPath || '';
      },
      error: (err) => {
        this.snackBar.open('An error occurred while loading the playlist.', 'Dismiss', { duration: 3000 });
      }
    });
  }

  createOrUpdatePlaylist() {
    const formData = new FormData();

    formData.append('title', this.playlist.title);
    formData.append('isPublic', this.playlist.isPublic ? 'true' : 'false');
    formData.append('trackIds', JSON.stringify(this.playlist.trackIds || []));

    if (this.playlist.coverImage) {
      formData.append('coverImage', this.playlist.coverImage, this.playlist.coverImage.name);
    } else if (this.existingCoverPath) {
      formData.append('coverImage', this.existingCoverPath);
    }

    if (this.playlist.id) {
      this.updatePlaylist(formData, this.playlist.id);
    } else {
      this.createPlaylist(formData);
    }
  }

  createPlaylist(formData: FormData) {
    const userId = this.getCurrentUserId(); // Funkcija za dohvaćanje trenutnog userId-a

    const playlistRequest: PlaylistAddRequest = {
      title: formData.get('title') as string,
      isPublic: formData.get('isPublic') === 'true',
      trackIds: JSON.parse(formData.get('trackIds') as string) || [],
      coverImage: formData.get('coverImage') as File,
      userId: userId
    };

    this.playlistCreateService.handleAsync(playlistRequest).subscribe({
      next: (response: any) => {
        const message = `Successfully created playlist ${response.title}!`;
        this.snackBar.open(message, 'Dismiss', { duration: 3000 });
        this.router.navigate(['/listener/playlist']);
      },
      error: (err: HttpErrorResponse) => {
        this.snackBar.open('An error occurred. Please try again.', 'Dismiss', { duration: 3000 });
      }
    });
  }

  getCurrentUserId(): number {
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




  updatePlaylist(formData: FormData, id: number) {
    console.log('Updating playlist with ID:', id);
    this.playlistUpdateService.handleAsync(id, formData).subscribe({
      next: (response: any) => {
        const message = `Successfully updated playlist ${response.title}!`;
        this.snackBar.open(message, 'Dismiss', { duration: 3000 });
        this.router.navigate(['/listener/playlist']);
        window.location.reload();
      },
      error: (err: HttpErrorResponse) => {
        this.snackBar.open('An error occurred while updating. Please try again.', 'Dismiss', { duration: 3000 });
      }
    });
  }

  selectCoverFile(e: File | undefined) {
    if (e) {
      this.playlist.coverImage = e;
    }
  }

  getPath() {
    return MyConfig.mediaUrl(this.playlist.coverImage ? this.playlist.coverImage.name : this.existingCoverPath);
  }
}
