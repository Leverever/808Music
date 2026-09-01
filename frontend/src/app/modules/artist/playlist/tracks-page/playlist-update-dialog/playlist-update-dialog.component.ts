import { Component, Inject, OnInit } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { FormGroup, FormBuilder, Validators } from '@angular/forms';
import { PlaylistUpdateEndpointService } from '../../../../../endpoints/playlist-endpoints/update-playlist-endpoint.service';
import { PlaylistByIdResponse } from '../../../../../endpoints/playlist-endpoints/get-playlist-by-id-endpoint.service';
import { MyConfig } from '../../../../../my-config';

@Component({
  selector: 'app-playlist-update-dialog',
  templateUrl: './playlist-update-dialog.component.html',
  styleUrls: ['./playlist-update-dialog.component.css'],
})
export class PlaylistUpdateDialogComponent implements OnInit {
  playlistForm: FormGroup;
  coverPath: string | null = null;
  newCoverFile: File | null = null;
  previewUrl: string | null = null;
  isNewCover: boolean = false;

  constructor(
    public dialogRef: MatDialogRef<PlaylistUpdateDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { playlistDetails: PlaylistByIdResponse },
    private playlistUpdateService: PlaylistUpdateEndpointService,
    private fb: FormBuilder
  ) {
    this.playlistForm = this.fb.group({
      title: [this.data.playlistDetails?.title, Validators.required],
      isPublic: [this.data.playlistDetails?.isPublic],
    });
  }

  ngOnInit(): void {
    if (this.data.playlistDetails?.coverPath) {
      this.coverPath = this.data.playlistDetails.coverPath;
      this.previewUrl = this.MyConfig.mediaUrl(this.coverPath);
    }
  }

  onFileSelected(event: any): void {
    const file: File = event.target.files[0];
    if (file) {
      this.newCoverFile = file;
      this.isNewCover = true;

      const reader = new FileReader();
      reader.onload = (e: any) => {
        this.previewUrl = e.target.result;
      };
      reader.readAsDataURL(file);
    }
  }

  saveChanges(): void {
    if (this.playlistForm.invalid) {
      return;
    }

    const formData = new FormData();
    formData.append('title', this.playlistForm.value.title);
    formData.append('isPublic', this.playlistForm.value.isPublic.toString());


    if (this.isNewCover && this.newCoverFile) {
      formData.append('CoverImage', this.newCoverFile, this.newCoverFile.name);
    } else if (this.coverPath) {
      formData.append('CoverImage', this.coverPath);
    }

    this.playlistUpdateService.handleAsync(this.data.playlistDetails.id, formData).subscribe({
      next: (response) => {
        console.log('Playlist updated successfully', response);
        this.dialogRef.close(response);
      },
      error: (err) => {
        console.error('Error updating playlist:', err);
      },
    });
  }

  onNoClick(): void {
    this.dialogRef.close();
  }

  triggerFileInput(): void {
    const fileInput = document.getElementById('coverImage') as HTMLInputElement;
    if (fileInput) {
      fileInput.click();
    }
  }

  protected readonly MyConfig = MyConfig;
}
