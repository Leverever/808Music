import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';
import {buildHttpParams} from '../../helper/http-params.helper';

export interface ReleaseTrackListRequest {
  pageNumber?: number;
  pageSize?: number;
  title?: string;
}

export interface ReleaseTrackArtistResponse {
  id: number;
  name: string;
  profilePhotoPath: string;
  isLead: boolean;
}

export interface ReleaseTrackResponse {
  associationId: number | null;
  releaseId: number;
  trackId: number;
  title: string;
  titleOverride: string | null;
  discNumber: number;
  trackNumber: number;
  isPrimaryRelease: boolean;
  isExplicit: boolean;
  length: number;
  streams: number;
  trackPath: string;
  coverPath: string;
  isLegacyAssociation: boolean;
  artists: ReleaseTrackArtistResponse[];
}

export interface ReleaseTrackPagedResponse {
  items: ReleaseTrackResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ReleaseTracksEndpointService {
  private readonly baseUrl = `${MyConfig.api_address}/api/v2/releases`;

  constructor(private httpClient: HttpClient) {
  }

  getByRelease(
    releaseId: number,
    request: ReleaseTrackListRequest
  ): Observable<ReleaseTrackPagedResponse> {
    return this.httpClient.get<ReleaseTrackPagedResponse>(
      `${this.baseUrl}/${releaseId}/tracks`,
      {params: buildHttpParams(request)}
    );
  }
}
