import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable, of, tap} from 'rxjs';
import {MyConfig} from '../../my-config';
import {RecommendationTrackDto} from './recommendation.models';
import {MyUserAuthService} from '../../services/auth-services/my-user-auth.service';

export interface TrackRadioResponse {
  seedTrackId: number;
  tracks: RecommendationTrackDto[];
}

interface TrackRadioCacheEntry {
  cachedAt: number;
  expiresAt: number;
  response: TrackRadioResponse;
}

@Injectable({providedIn: 'root'})
export class TrackRadioEndpointService {
  private readonly cachePrefix = '808music:track-radio:v1:';
  private readonly cacheTtlMs = 24 * 60 * 60 * 1000;
  private readonly maxCachedRadios = 30;

  constructor(
    private httpClient: HttpClient,
    private authService: MyUserAuthService
  ) {}

  handleAsync(trackId: number, limit = 50): Observable<TrackRadioResponse> {
    const cacheKey = this.getCacheKey(trackId, limit);
    const cachedRadio = this.readCache(cacheKey);
    if(cachedRadio)
    {
      return of(cachedRadio);
    }

    return this.httpClient.get<TrackRadioResponse>(
      `${MyConfig.api_address}/api/v2/tracks/${trackId}/radio`,
      {params: {limit}}
    ).pipe(
      tap(response => this.writeCache(cacheKey, response))
    );
  }

  private getCacheKey(trackId: number, limit: number): string {
    const userId = this.authService.getAuthToken(true)?.userId ?? 'anonymous';
    return `${this.cachePrefix}${userId}:${trackId}:${limit}`;
  }

  private readCache(cacheKey: string): TrackRadioResponse | null {
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

      const entry = JSON.parse(serializedEntry) as Partial<TrackRadioCacheEntry>;
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

  private writeCache(cacheKey: string, response: TrackRadioResponse): void {
    if(typeof window === 'undefined')
    {
      return;
    }

    const now = Date.now();
    const entry: TrackRadioCacheEntry = {
      cachedAt: now,
      expiresAt: now + this.cacheTtlMs,
      response
    };

    try
    {
      this.pruneCache();
      window.localStorage.setItem(cacheKey, JSON.stringify(entry));
    }
    catch
    {
      // A storage quota or privacy setting should never stop a radio from loading.
    }
  }

  private pruneCache(): void {
    const cachedEntries: Array<{key: string; cachedAt: number; expired: boolean}> = [];
    const now = Date.now();

    for(let index = window.localStorage.length - 1; index >= 0; index--)
    {
      const key = window.localStorage.key(index);
      if(!key?.startsWith(this.cachePrefix))
      {
        continue;
      }

      try
      {
        const entry = JSON.parse(window.localStorage.getItem(key) ?? '') as Partial<TrackRadioCacheEntry>;
        cachedEntries.push({
          key,
          cachedAt: typeof entry.cachedAt === 'number' ? entry.cachedAt : 0,
          expired: typeof entry.expiresAt !== 'number' || entry.expiresAt <= now
        });
      }
      catch
      {
        cachedEntries.push({key, cachedAt: 0, expired: true});
      }
    }

    cachedEntries
      .filter(entry => entry.expired)
      .forEach(entry => window.localStorage.removeItem(entry.key));

    cachedEntries
      .filter(entry => !entry.expired)
      .sort((first, second) => second.cachedAt - first.cachedAt)
      .slice(this.maxCachedRadios - 1)
      .forEach(entry => window.localStorage.removeItem(entry.key));
  }
}
