import {Component, Inject, inject} from '@angular/core';
import {MAT_BOTTOM_SHEET_DATA, MatBottomSheetRef} from '@angular/material/bottom-sheet';
import {
  PlaylistResponse
} from '../../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {MyConfig} from '../../../../my-config';

export interface AddToPlaylistBottomSheetData {
  playlists: PlaylistResponse[];
  membership: Record<number, boolean>;
}

export interface AddToPlaylistBottomSheetResult {
  playlistId: number;
}

@Component({
  selector: 'app-add-to-playlist-bottom-sheet',
  templateUrl: './add-to-playlist-bottom-sheet.component.html',
  styleUrl: './add-to-playlist-bottom-sheet.component.css'
})
export class AddToPlaylistBottomSheetComponent {
  private sheetRef =
    inject<MatBottomSheetRef<AddToPlaylistBottomSheetComponent, AddToPlaylistBottomSheetResult>>(
      MatBottomSheetRef
    );

  constructor(
    @Inject(MAT_BOTTOM_SHEET_DATA)
    protected data: AddToPlaylistBottomSheetData
  ) {}

  dismiss(): void {
    this.sheetRef.dismiss();
  }

  selectPlaylist(playlistId: number): void {
    this.sheetRef.dismiss({playlistId});
  }

  coverUrl(path: string): string {
    if(/^https?:\/\//i.test(path))
    {
      return path;
    }

    return `${MyConfig.media_address}${path}`;
  }
}
