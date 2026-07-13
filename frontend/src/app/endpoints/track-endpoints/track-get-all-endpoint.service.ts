import { Injectable } from '@angular/core';
import {MyConfig} from '../../my-config';
import {MyBaseEndpointAsync} from '../../helper/my-base-endpoint-async.interface';
import {TrackGetResponse} from './track-get-by-id-endpoint.service';
import {HttpClient} from '@angular/common/http';
import {map, Observable} from 'rxjs';
import {buildHttpParams} from '../../helper/http-params.helper';
import {MyPagedList} from '../../services/auth-services/dto/my-paged-list';
import {
  ReleaseTrackResponse,
  ReleaseTracksEndpointService
} from '../release-track-endpoints/release-tracks-endpoint.service';
export type { TrackGetResponse } from './track-get-by-id-endpoint.service';

export interface TrackGetAllRequest {
  pageNumber?: number;
  pageSize?: number;
  albumId?: number
  leadArtistId?: number
  featuredArtists?: number[]
  title?: string
  isReleased?: boolean
  sortByStreams?: boolean
}

@Injectable({
  providedIn: 'root'
})
export class TrackGetAllEndpointService implements MyBaseEndpointAsync<TrackGetAllRequest, MyPagedList<TrackGetResponse>> {
  readonly url = `${MyConfig.api_address}/api/TrackGetAllEndpoint`;

  constructor(
    private httpClient: HttpClient,
    private releaseTracksEndpointService: ReleaseTracksEndpointService
  ) {
  }

  handleAsync(request: TrackGetAllRequest): Observable<MyPagedList<TrackGetResponse>> {
      if (request.albumId != null) {
        return this.releaseTracksEndpointService.getByRelease(
          request.albumId,
          {
            pageNumber: request.pageNumber,
            pageSize: Math.min(request.pageSize ?? 20, 500),
            title: request.title
          }
        ).pipe(map(response => ({
          dataItems: response.items.map(item => this.toLegacyTrackResponse(item)),
          currentPage: response.page,
          totalPages: response.totalPages,
          pageSize: response.pageSize,
          totalCount: response.totalCount,
          hasPrevious: response.hasPreviousPage,
          hasNext: response.hasNextPage
        })));
      }

      let params = buildHttpParams(request);
      return this.httpClient.get<MyPagedList<TrackGetResponse>>(this.url, {params});
    }

  private toLegacyTrackResponse(item: ReleaseTrackResponse): TrackGetResponse {
    return {
      id: item.trackId,
      title: item.title,
      length: item.length,
      streams: item.streams,
      isExplicit: item.isExplicit,
      coverPath: this.toMediaPath(
        item.coverPath,
        'AlbumCovers',
        '/media/Images/playlist_placeholder.png'
      ),
      trackUserInfo: [],
      artists: item.artists.map(artist => ({
        id: artist.id,
        name: artist.name,
        pfpPath: this.toMediaPath(
          artist.profilePhotoPath,
          'ArtistPfps',
          '/media/Images/playlist_placeholder.png'
        ),
        isLead: artist.isLead
      })),
      albumId: item.releaseId
    };
  }

  private toMediaPath(path: string, folder: string, fallback: string): string {
    if (!path) {
      return fallback;
    }

    return path.startsWith('/')
      ? path
      : `/media/Images/${folder}/${path}`;
  }

}

