import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';
import {RecommendationTrackDto} from './recommendation.models';

export interface TrackRadioResponse {
  seedTrackId: number;
  tracks: RecommendationTrackDto[];
}

@Injectable({providedIn: 'root'})
export class TrackRadioEndpointService {
  constructor(private httpClient: HttpClient) {}

  handleAsync(trackId: number, limit = 50): Observable<TrackRadioResponse> {
    return this.httpClient.get<TrackRadioResponse>(
      `${MyConfig.api_address}/api/v2/tracks/${trackId}/radio`,
      {params: {limit}}
    );
  }
}
