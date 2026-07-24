import { Component, ElementRef, Inject, Optional, ViewChild } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { FormGroup, FormBuilder, Validators } from '@angular/forms';
import { PlaylistCreateService } from '../../../../../endpoints/playlist-endpoints/create-playlist-endpoint.service';
import { finalize } from 'rxjs';
import { PlaylistUpdateEndpointService } from '../../../../../endpoints/playlist-endpoints/update-playlist-endpoint.service';
import { PlaylistByIdResponse } from '../../../../../endpoints/playlist-endpoints/get-playlist-by-id-endpoint.service';
import { MyConfig } from '../../../../../my-config';

export interface PlaylistCreateDialogData {
  playlistDetails?: Pick<PlaylistByIdResponse, 'id' | 'title' | 'isPublic' | 'coverPath'>;
}

@Component({
  selector: 'app-playlist-create-dialog',
  templateUrl: './playlist-create-dialog.component.html',
  styleUrls: ['./playlist-create-dialog.component.css'],
})
export class PlaylistCreateDialogComponent {
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  playlistForm: FormGroup;
  previewUrl: string | null = null;
  newCoverFile: File | null = null;
  isSubmitting = false;
  createError = '';

  readonly isEditMode: boolean;

  constructor(
    public dialogRef: MatDialogRef<PlaylistCreateDialogComponent>,
    private fb: FormBuilder,
    private playlistCreateService: PlaylistCreateService,
    private playlistUpdateService: PlaylistUpdateEndpointService,
    @Optional() @Inject(MAT_DIALOG_DATA) public data: PlaylistCreateDialogData | null,
  ) {
    const playlist = data?.playlistDetails;
    this.isEditMode = Boolean(playlist);
    this.playlistForm = this.fb.group({
      title: [playlist?.title || '', Validators.required],
      isPublic: [playlist?.isPublic ?? false],
    });

    if (playlist?.coverPath) {
      this.previewUrl = `${MyConfig.media_address}${playlist.coverPath}`;
    }
  }

  get dialogTitle(): string {
    return this.isEditMode ? 'Edit playlist' : 'Create a playlist';
  }

  get dialogDescription(): string {
    return this.isEditMode
      ? 'Update the name, visibility or artwork for this playlist.'
      : 'Give your next collection a name and a cover of its own.';
  }

  get submitLabel(): string {
    if (this.isSubmitting) {
      return this.isEditMode ? 'Saving...' : 'Creating...';
    }

    return this.isEditMode ? 'Save changes' : 'Create playlist';
  }

  get titleLength(): number {
    return String(this.playlistForm.get('title')?.value || '').length;
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.newCoverFile = file;

      const reader = new FileReader();
      reader.onload = (e: any) => {
        this.previewUrl = e.target.result;
      };
      reader.readAsDataURL(file);
    }
  }

  submitPlaylist(): void {
    const titleControl = this.playlistForm.get('title');
    const title = String(titleControl?.value || '').trim();

    if (!title) {
      titleControl?.setErrors({required: true});
    }

    if (this.playlistForm.invalid || this.isSubmitting) {
      this.playlistForm.markAllAsTouched();
      return;
    }

    this.createError = '';
    this.isSubmitting = true;

    const request$ = this.isEditMode
      ? this.updatePlaylist(title)
      : this.playlistCreateService.handleAsync({
          title,
          isPublic: this.playlistForm.value.isPublic,
          coverImage: this.newCoverFile || undefined,
          trackIds: [],
          userId: this.getUserIdFromToken(),
        });

    request$.pipe(
      finalize(() => this.isSubmitting = false)
    ).subscribe({
      next: (response) => {
        this.dialogRef.close(response);
      },
      error: (err) => {
        console.error(`Error ${this.isEditMode ? 'updating' : 'creating'} playlist:`, err);
        this.createError = `We could not ${this.isEditMode ? 'update' : 'create'} this playlist. Please try again.`;
      },
    });
  }

  private updatePlaylist(title: string) {
    const formData = new FormData();
    formData.append('title', title);
    formData.append('isPublic', String(Boolean(this.playlistForm.value.isPublic)));

    if (this.newCoverFile) {
      formData.append('CoverImage', this.newCoverFile, this.newCoverFile.name);
    }

    return this.playlistUpdateService.handleAsync(this.data!.playlistDetails!.id, formData);
  }


  onNoClick(): void {
    this.dialogRef.close();
  }

  triggerFileInput(): void {
    this.fileInput?.nativeElement.click();
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
}
