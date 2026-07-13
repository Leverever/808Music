import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';

export type TrackInteractionType =
  | 'PlayStarted'
  | 'PlayCompleted'
  | 'Skipped'
  | 'Liked'
  | 'Unliked'
  | 'AddedToPlaylist'
  | 'RemovedFromPlaylist';

export type TrackInteractionContext =
  | 'Playback'
  | 'Autoplay'
  | 'Radio'
  | 'Playlist'
  | 'Manual';

export interface RecordTrackInteractionRequest {
  trackId: number;
  interactionType: TrackInteractionType;
  playedMs?: number;
  trackDurationMs?: number;
  contextType?: TrackInteractionContext;
  clientEventId: string;
  occurredAt: string;
}

export interface RecordTrackInteractionResponse {
  interactionId: string;
  created: boolean;
  occurredAt: string;
}

@Injectable({providedIn: 'root'})
export class TrackInteractionEndpointService {
  private readonly url = `${MyConfig.api_address}/api/v2/me/track-interactions`;

  constructor(private httpClient: HttpClient) {}

  handleAsync(request: RecordTrackInteractionRequest): Observable<RecordTrackInteractionResponse> {
    return this.httpClient.post<RecordTrackInteractionResponse>(this.url, request);
  }
}
