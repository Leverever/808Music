import { Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { MyConfig } from '../../my-config';
import {TrackInteractionService} from '../../services/personalization/track-interaction.service';

export interface PlaylistUpdateTracksRequest {
  playlistId: number;
  trackIds: number[];
  userId : number;
}

@Injectable({
  providedIn: 'root',
})
export class PlaylistUpdateTracksService {
  private readonly url = `${MyConfig.api_address}/api/playlists/update-tracks`;

  constructor(
    private httpClient: HttpClient,
    private interactions: TrackInteractionService
  ) {}

  handleAsync(request: PlaylistUpdateTracksRequest): Observable<void> {
    return this.httpClient.post<void>(this.url, request).pipe(
      tap(() => request.trackIds.forEach(trackId =>
        this.interactions.record(trackId, 'AddedToPlaylist', {contextType: 'Playlist'})))
    );
  }
}
