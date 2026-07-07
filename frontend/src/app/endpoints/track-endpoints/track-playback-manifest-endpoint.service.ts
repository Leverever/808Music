import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';
import {buildHttpParams} from '../../helper/http-params.helper';
import {MyBaseEndpointAsync} from '../../helper/my-base-endpoint-async.interface';

export interface TrackPlaybackManifestRequest {
  trackId: number;
  artistMode?: boolean;
}

export interface PlaybackArtistDto {
  id: number;
  name: string;
  isLead: boolean;
  role: string;
}

export interface PlaybackTrackDto {
  id: number;
  title: string;
  isExplicit: boolean;
  lengthSeconds: number;
  streams: number;
  artists: PlaybackArtistDto[];
}

export interface PlaybackAssetDto {
  name: string;
  contentType: string;
  url: string;
}

export interface PlaybackStemSetDto {
  id: string;
  source: string;
  stemProfile: string;
  stems: PlaybackAssetDto[];
}

export interface PlaybackStreamDto {
  expiresAt: string;
  master: PlaybackAssetDto;
  stemSet: PlaybackStemSetDto | null;
}

export interface TrackPlaybackManifestResponse {
  track: PlaybackTrackDto;
  stream: PlaybackStreamDto;
}

@Injectable({
  providedIn: 'root'
})
export class TrackPlaybackManifestEndpointService
  implements MyBaseEndpointAsync<TrackPlaybackManifestRequest, TrackPlaybackManifestResponse> {

  constructor(private httpClient: HttpClient) {
  }

  handleAsync(request: TrackPlaybackManifestRequest): Observable<TrackPlaybackManifestResponse> {
    const params = buildHttpParams({artistMode: request.artistMode ?? false});

    return this.httpClient.get<TrackPlaybackManifestResponse>(
      `${MyConfig.api_address}/api/v2/tracks/${request.trackId}/playback`,
      {params}
    );
  }
}
