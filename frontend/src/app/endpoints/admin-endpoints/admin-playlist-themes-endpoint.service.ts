import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';

export type PlaylistThemeLabelPolarity = 'Positive' | 'Negative';
export type PlaylistThemeLabelSource = 'EssentiaTag' | 'ClapText';

export interface AdminPlaylistThemeLabel {
  id?: string;
  label: string;
  polarity: PlaylistThemeLabelPolarity;
  source: PlaylistThemeLabelSource;
  tagNamespace: string | null;
  weight: number;
}

export interface AdminPlaylistThemeTagNamespace {
  namespace: string;
  labels: string[];
}

export interface AdminPlaylistTheme {
  id: string;
  themeKey: string;
  name: string;
  description: string;
  isActive: boolean;
  trackCount: number;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
  labels: AdminPlaylistThemeLabel[];
}

export interface CreateAdminPlaylistThemeRequest {
  themeKey: string;
  name: string;
  description: string;
  isActive: boolean;
  trackCount: number;
  sortOrder: number;
  labels: AdminPlaylistThemeLabel[];
}

export type UpdateAdminPlaylistThemeRequest =
  Omit<CreateAdminPlaylistThemeRequest, 'themeKey'>;

@Injectable({providedIn: 'root'})
export class AdminPlaylistThemesEndpointService {
  private readonly url = `${MyConfig.api_address}/api/v2/admin/playlist-themes`;

  constructor(private http: HttpClient) {
  }

  list(): Observable<AdminPlaylistTheme[]> {
    return this.http.get<AdminPlaylistTheme[]>(this.url);
  }

  get(id: string): Observable<AdminPlaylistTheme> {
    return this.http.get<AdminPlaylistTheme>(
      `${this.url}/${encodeURIComponent(id)}`
    );
  }

  getTagCatalog(): Observable<AdminPlaylistThemeTagNamespace[]> {
    return this.http.get<AdminPlaylistThemeTagNamespace[]>(
      `${this.url}/tag-catalog`
    );
  }

  create(request: CreateAdminPlaylistThemeRequest): Observable<AdminPlaylistTheme> {
    return this.http.post<AdminPlaylistTheme>(this.url, request);
  }

  update(
    id: string,
    request: UpdateAdminPlaylistThemeRequest
  ): Observable<AdminPlaylistTheme> {
    return this.http.put<AdminPlaylistTheme>(
      `${this.url}/${encodeURIComponent(id)}`,
      request
    );
  }

  setActive(id: string, isActive: boolean): Observable<AdminPlaylistTheme> {
    return this.http.patch<AdminPlaylistTheme>(
      `${this.url}/${encodeURIComponent(id)}/active`,
      {isActive}
    );
  }
}
