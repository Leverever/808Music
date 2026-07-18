import { Component, ElementRef, ViewChild } from '@angular/core';
import { MatDialogRef } from '@angular/material/dialog';
import { FormGroup, FormBuilder, Validators } from '@angular/forms';
import { PlaylistCreateService } from '../../../../../endpoints/playlist-endpoints/create-playlist-endpoint.service';
import { finalize } from 'rxjs';

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

  constructor(
    public dialogRef: MatDialogRef<PlaylistCreateDialogComponent>,
    private fb: FormBuilder,
    private playlistCreateService: PlaylistCreateService
  ) {
    this.playlistForm = this.fb.group({
      title: ['', Validators.required],
      isPublic: [false],
    });
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

  createPlaylist(): void {
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

    const request = {
      title,
      isPublic: this.playlistForm.value.isPublic,
      coverImage: this.newCoverFile || undefined,
      trackIds: [],
      userId: this.getUserIdFromToken(),
    };

    this.playlistCreateService.handleAsync(request).pipe(
      finalize(() => this.isSubmitting = false)
    ).subscribe({
      next: (response) => {
        console.log('Playlist created successfully', response);
        this.dialogRef.close(response);
      },
      error: (err) => {
        console.error('Error creating playlist:', err);
        this.createError = 'We could not create this playlist. Please try again.';
      },
    });
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
