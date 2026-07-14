import { Injectable } from '@angular/core';
import {MyBaseEndpointAsync} from '../../helper/my-base-endpoint-async.interface';
import { Observable } from 'rxjs';
import {MyConfig} from '../../my-config';
import {HttpClient} from '@angular/common/http';

export interface ArtistTrackDto {
  id: number;
  name: string;
  pfpPath: string;
  isLead: boolean;
}
export interface TrackUserInfoDto{
  addedByUsername : string;
  addedByUserProfilePhotoPath : string;
}
export interface TrackGetResponse {
  id: number;
  title: string;
  length: number;
  streams: number;
  isExplicit: boolean;
  coverPath: string;
  trackUserInfo : TrackUserInfoDto[];
  artists: ArtistTrackDto[];
  albumId: number | null;
  associationId?: number | null;
  discNumber?: number;
  trackNumber?: number;
  isLegacyAssociation?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class TrackGetByIdEndpointService implements MyBaseEndpointAsync<number, TrackGetResponse> {
  readonly url = `${MyConfig.api_address}/api/TrackGetByIdEndpoint`;
  constructor(private httpClient: HttpClient) {
  }

  handleAsync(id: number): Observable<TrackGetResponse> {
    return this.httpClient.get<TrackGetResponse>(this.url+"/"+id);
  }
}
