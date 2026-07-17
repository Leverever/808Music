import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable, of, tap} from 'rxjs';
import {MyConfig} from '../../my-config';
import {RecommendationTrackDto} from './recommendation.models';
import {MyUserAuthService} from '../../services/auth-services/my-user-auth.service';

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

interface HomeRecommendationsCacheEntry {
  expiresAt: number;
  response: HomeRecommendationsResponse;
}

@Injectable({providedIn: 'root'})
export class HomeRecommendationsEndpointService {
  private readonly url = `${MyConfig.api_address}/api/v2/recommendations/home`;
  private readonly cachePrefix = '808music:home-recommendations:v1:';
  private readonly cacheTtlMs = 15 * 60 * 1000;

  constructor(
    private httpClient: HttpClient,
    private authService: MyUserAuthService
  ) {}

  handleAsync(): Observable<HomeRecommendationsResponse> {
    const cacheKey = this.getCacheKey();
    const cachedRecommendations = this.readCache(cacheKey);
    if(cachedRecommendations)
    {
      return of(cachedRecommendations);
    }

    return this.httpClient.get<HomeRecommendationsResponse>(this.url).pipe(
      tap(response => this.writeCache(cacheKey, response))
    );
  }

  private getCacheKey(): string {
    const userId = this.authService.getAuthToken(true)?.userId ?? 'anonymous';
    return `${this.cachePrefix}${userId}`;
  }

  private readCache(cacheKey: string): HomeRecommendationsResponse | null {
    if(typeof window === 'undefined')
    {
      return null;
    }

    try
    {
      const serializedEntry = window.localStorage.getItem(cacheKey);
      if(!serializedEntry)
      {
        return null;
      }

      const entry = JSON.parse(serializedEntry) as Partial<HomeRecommendationsCacheEntry>;
      if(typeof entry.expiresAt !== 'number' || entry.expiresAt <= Date.now() || !entry.response)
      {
        window.localStorage.removeItem(cacheKey);
        return null;
      }

      return entry.response;
    }
    catch
    {
      return null;
    }
  }

  private writeCache(cacheKey: string, response: HomeRecommendationsResponse): void {
    if(typeof window === 'undefined')
    {
      return;
    }

    const entry: HomeRecommendationsCacheEntry = {
      expiresAt: Date.now() + this.cacheTtlMs,
      response
    };

    try
    {
      window.localStorage.setItem(cacheKey, JSON.stringify(entry));
    }
    catch
    {
      // Storage restrictions should never prevent recommendations from loading.
    }
  }
}
