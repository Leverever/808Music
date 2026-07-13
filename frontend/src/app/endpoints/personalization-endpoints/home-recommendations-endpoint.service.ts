import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';
import {RecommendationTrackDto} from './recommendation.models';

export interface HomeDailyPlaylistRecommendation {
  playlistId: string;
  themeKey: string;
  name: string;
  description: string;
  coverPath: string;
  playlistDate: string;
  createdAt: string;
  trackCount: number;
  score: number;
  reason: string;
}

export interface HomeAlbumRecommendation {
  albumId: number;
  title: string;
  coverPath: string;
  artistId: number;
  artistName: string;
  trackCount: number;
  score: number;
  reason: string;
  matchedTrackIds: number[];
}

export interface HomeArtistRecommendation {
  artistId: number;
  name: string;
  profilePhotoPath: string;
  score: number;
  reason: string;
  matchedTrackIds: number[];
}

export interface HomePlaylistRecommendation {
  playlistId: number;
  title: string;
  coverPath: string;
  isPublic: boolean;
  isCollaborative: boolean;
  trackCount: number;
  ownerUserId: number | null;
  ownerUsername: string | null;
  score: number;
  reason: string;
  matchedTrackIds: number[];
}

export interface HomeRecommendationsResponse {
  recommendationDate: string;
  dailyPersonalizedPlaylists: HomeDailyPlaylistRecommendation[];
  recommendedAlbums: HomeAlbumRecommendation[];
  recommendedArtists: HomeArtistRecommendation[];
  recommendedPlaylists: HomePlaylistRecommendation[];
  recommendedTracks: RecommendationTrackDto[];
}

@Injectable({providedIn: 'root'})
export class HomeRecommendationsEndpointService {
  private readonly url = `${MyConfig.api_address}/api/v2/recommendations/home`;

  constructor(private httpClient: HttpClient) {}

  handleAsync(): Observable<HomeRecommendationsResponse> {
    return this.httpClient.get<HomeRecommendationsResponse>(this.url);
  }
}
