import {Injectable} from '@angular/core';
import {HttpClient, HttpEvent} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';
import {buildHttpParams} from '../../helper/http-params.helper';

export interface V2PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface TrackArtistV2 {
  artistTrackId: number;
  artistId: number;
  name: string;
  profilePhotoPath: string;
  isLead: boolean;
  showOnProfile: boolean;
}

export interface TrackReleaseV2 {
  associationId: number | null;
  releaseId: number;
  title: string;
  coverPath: string;
  releaseDate: string;
  releaseType: string;
  discNumber: number;
  trackNumber: number;
  titleOverride: string | null;
  isPrimaryRelease: boolean;
  isLegacyAssociation: boolean;
}

export interface TrackCatalogItemV2 {
  id: number;
  title: string;
  isExplicit: boolean;
  lengthSeconds: number;
  streams: number;
  featuredArtists: TrackArtistV2[];
  releaseCount: number;
  primaryRelease: {releaseId: number; title: string; coverPath: string} | null;
}

export interface TrackDetailsV2 {
  id: number;
  title: string;
  isExplicit: boolean;
  lengthSeconds: number;
  streams: number;
  leadArtist: TrackArtistV2;
  featuredArtists: TrackArtistV2[];
  releases: TrackReleaseV2[];
  analysis: {
    status: string;
    errorMessage: string | null;
    tags: {namespace: string; label: string; score: number}[];
  } | null;
}

export interface ArtistSearchV2 {
  id: number;
  name: string;
  profilePhotoPath: string;
}

export interface ReleaseSearchV2 {
  id: number;
  title: string;
  coverPath: string;
  releaseDate: string;
  releaseType: string;
}

export interface TrackStemItemV2 {
  id: string;
  name: string;
  contentType: string;
  sizeBytes: number;
  streamUri: string;
}

export interface TrackStemSetV2 {
  id: string;
  source: 'AiGenerated' | 'ArtistUploaded' | string;
  status: 'Pending' | 'Processing' | 'Ready' | 'Failed' | string;
  stemProfile: string;
  isActive: boolean;
  createdAt: string;
  completedAt: string | null;
  errorMessage: string | null;
  stems: TrackStemItemV2[];
}

export interface TrackStemsV2 {
  trackId: number;
  stemSets: TrackStemSetV2[];
}

export interface TrackStatisticsV2 {
  trackId: number;
  days: number;
  from: string;
  to: string;
  allTimeStreams: number;
  estimatedAllTimeStreamedSeconds: number;
  allTimeUniqueListeners: number;
  periodStreams: number;
  periodUniqueListeners: number;
  dailyStreams: {date: string; streams: number}[];
}

export interface TrackUploadResultV2 {
  id: number;
  title: string;
  isExplicit: boolean;
  mainArtistId: number;
  objectKey: string;
}

@Injectable({providedIn: 'root'})
export class TrackManagementV2EndpointService {
  private readonly tracksUrl = `${MyConfig.api_address}/api/v2/tracks`;

  constructor(private http: HttpClient) {}

  listArtistTracks(
    artistId: number,
    request: {
      pageNumber: number;
      pageSize: number;
      title?: string;
      primaryReleaseTitle?: string;
      minStreams?: number;
      maxStreams?: number;
      minDurationSeconds?: number;
      maxDurationSeconds?: number;
      sortBy?: 'title' | 'primaryRelease' | 'duration' | 'streams';
      sortDirection?: 'asc' | 'desc';
    }
  ): Observable<V2PagedResponse<TrackCatalogItemV2>> {
    return this.http.get<V2PagedResponse<TrackCatalogItemV2>>(
      `${MyConfig.api_address}/api/v2/artists/${artistId}/tracks`,
      {params: buildHttpParams(request)}
    );
  }

  getDetails(trackId: number): Observable<TrackDetailsV2> {
    return this.http.get<TrackDetailsV2>(`${this.tracksUrl}/${trackId}`);
  }

  upload(artistId: number, title: string, isExplicit: boolean, masterFile: File): Observable<TrackUploadResultV2> {
    const formData = new FormData();
    formData.append('artistId', artistId.toString());
    formData.append('title', title);
    formData.append('isExplicit', isExplicit.toString());
    formData.append('masterFile', masterFile);
    return this.http.post<TrackUploadResultV2>(`${this.tracksUrl}/upload`, formData);
  }

  updateMetadata(trackId: number, title: string, isExplicit: boolean): Observable<unknown> {
    return this.http.put(`${this.tracksUrl}/${trackId}/metadata`, {title, isExplicit});
  }

  replaceMaster(trackId: number, masterFile: File): Observable<HttpEvent<unknown>> {
    const formData = new FormData();
    formData.append('masterFile', masterFile);
    return this.http.put(`${this.tracksUrl}/${trackId}/master`, formData, {
      observe: 'events',
      reportProgress: true
    });
  }

  replaceFeaturedArtists(
    trackId: number,
    artists: {artistId: number; showOnProfile: boolean}[]
  ): Observable<TrackArtistV2[]> {
    return this.http.put<TrackArtistV2[]>(`${this.tracksUrl}/${trackId}/featured-artists`, {artists});
  }

  replaceReleases(
    trackId: number,
    releases: {releaseId: number; discNumber: number; trackNumber: number; titleOverride: string | null; isPrimaryRelease: boolean}[]
  ): Observable<TrackReleaseV2[]> {
    return this.http.put<TrackReleaseV2[]>(`${this.tracksUrl}/${trackId}/releases`, {releases});
  }

  searchArtists(query: string, excludeArtistId: number): Observable<ArtistSearchV2[]> {
    return this.http.get<ArtistSearchV2[]>(`${MyConfig.api_address}/api/v2/artists/search`, {
      params: buildHttpParams({query, excludeArtistId, limit: 10})
    });
  }

  searchReleases(
    artistId: number,
    title: string,
    excludeTrackId: number
  ): Observable<V2PagedResponse<ReleaseSearchV2>> {
    return this.http.get<V2PagedResponse<ReleaseSearchV2>>(`${MyConfig.api_address}/api/v2/releases`, {
      params: buildHttpParams({artistId, title, excludeTrackId, pageNumber: 1, pageSize: 10})
    });
  }

  getStems(trackId: number): Observable<TrackStemsV2> {
    return this.http.get<TrackStemsV2>(`${this.tracksUrl}/${trackId}/stems`);
  }

  separateStems(trackId: number): Observable<unknown> {
    return this.http.post(`${this.tracksUrl}/${trackId}/stems/separate`, {});
  }

  activateStemSet(trackId: number, stemSetId: string): Observable<TrackStemSetV2> {
    return this.http.put<TrackStemSetV2>(
      `${this.tracksUrl}/${trackId}/stems/${stemSetId}/activate`,
      {}
    );
  }

  deleteStemSet(trackId: number, stemSetId: string): Observable<void> {
    return this.http.delete<void>(`${this.tracksUrl}/${trackId}/stems/${stemSetId}`);
  }

  uploadStemSet(
    trackId: number,
    stemProfile: string,
    files: Partial<Record<'vocals' | 'drums' | 'bass' | 'other' | 'instrumental', File>>
  ): Observable<unknown> {
    const formData = new FormData();
    formData.append('stemProfile', stemProfile);
    Object.entries(files).forEach(([name, file]) => {
      if (file) formData.append(name, file);
    });
    return this.http.post(`${this.tracksUrl}/${trackId}/stems/upload`, formData);
  }

  getStatistics(trackId: number, days: 7 | 30 | 90 | 365): Observable<TrackStatisticsV2> {
    return this.http.get<TrackStatisticsV2>(`${this.tracksUrl}/${trackId}/statistics`, {
      params: buildHttpParams({days})
    });
  }
}
