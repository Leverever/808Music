import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { HttpClient } from '@angular/common/http';
import { MyConfig } from '../../my-config';

export interface PlaylistResponse {
  id: number;
  title: string;
  numOfTracks: number;
  isPublic: boolean;
  coverPath: string;
  username: string
  isLikedSongs : boolean;
  userId : number;
  ownerUsername : string;
  isCollaborative : boolean;
  description?: string;
}

@Injectable({
  providedIn: 'root',
})
export class GetPlaylistsByUserIdEndpointService {
  private readonly url = `${MyConfig.api_address}/api/playlists/user`;

  constructor(private httpClient: HttpClient) {}

  handleAsync(userId: number): Observable<PlaylistResponse[]> {
    return this.httpClient.get<PlaylistResponse[]>(`${this.url}/${userId}`);
  }
}
