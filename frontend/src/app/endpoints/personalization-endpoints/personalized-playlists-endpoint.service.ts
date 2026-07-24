import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';
import {RecommendationArtistDto, RecommendationTrackDto} from './recommendation.models';

export interface PersonalizedPlaylistSummary {
  id: string;
  themeKey: string;
  name: string;
  description: string;
  coverPath?: string;
  playlistDate: string;
  createdAt: string;
  trackCount: number;
}

export interface DailyPersonalizedPlaylistsResponse {
  playlistDate: string;
  playlists: PersonalizedPlaylistSummary[];
}

export interface PersonalizedPlaylistTrack extends RecommendationTrackDto {
  position: number;
  score: number;
  reason: string;
  artists: RecommendationArtistDto[];
}

export interface PersonalizedPlaylistDetail {
  id: string;
  themeKey: string;
  name: string;
  description: string;
  coverPath?: string;
  playlistDate: string;
  createdAt: string;
  tracks: PersonalizedPlaylistTrack[];
}

@Injectable({providedIn: 'root'})
export class PersonalizedPlaylistsEndpointService {
  private readonly url = `${MyConfig.api_address}/api/v2/personalized-playlists`;

  constructor(private httpClient: HttpClient) {}

  getDaily(): Observable<DailyPersonalizedPlaylistsResponse> {
    return this.httpClient.get<DailyPersonalizedPlaylistsResponse>(`${this.url}/daily`);
  }

  getById(id: string): Observable<PersonalizedPlaylistDetail> {
    return this.httpClient.get<PersonalizedPlaylistDetail>(`${this.url}/${id}`);
  }
}
