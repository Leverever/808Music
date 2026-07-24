import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { MyConfig } from '../../my-config';
import {buildHttpParams} from '../../helper/http-params.helper';
import type {PlaylistResponse} from './get-playlist-by-user-endpoint.service';
export type {PlaylistResponse} from './get-playlist-by-user-endpoint.service';

export interface PlaylistSearchRequest {
  searchString?: string;
  returnAmount?: number;
  publicOnly?: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class GetAllPlaylistsService {
  private readonly url = `${MyConfig.api_address}/api/playlists`;

  constructor(private httpClient: HttpClient) {}

  handleAsync(request: PlaylistSearchRequest = {}): Observable<PlaylistResponse[]> {
    return this.httpClient.get<PlaylistResponse[]>(this.url, {
      params: buildHttpParams(request)
    });
  }
}
