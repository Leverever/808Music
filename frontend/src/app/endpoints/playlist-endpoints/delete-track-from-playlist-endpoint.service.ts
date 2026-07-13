import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { MyConfig } from '../../my-config';
import {TrackInteractionService} from '../../services/personalization/track-interaction.service';

@Injectable({
  providedIn: 'root',
})
export class RemoveTrackFromPlaylistService {
  private readonly url = `${MyConfig.api_address}/api/playlists`;

  constructor(
    private httpClient: HttpClient,
    private interactions: TrackInteractionService
  ) {}

  handleAsync(playlistId: number, trackId: number): Observable<void> {
    return this.httpClient.delete<void>(`${this.url}/${playlistId}/tracks/${trackId}`).pipe(
      tap(() => this.interactions.record(
        trackId,
        'RemovedFromPlaylist',
        {contextType: 'Playlist'}))
    );
  }
}
