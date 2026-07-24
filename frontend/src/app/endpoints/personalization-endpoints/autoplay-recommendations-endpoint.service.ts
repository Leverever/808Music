import {Injectable} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';
import {RecommendationTrackDto} from './recommendation.models';

export interface AutoplayRecommendationsRequest {
  seedTrackIds: number[];
  excludedTrackIds: number[];
  limit?: number;
}

export interface AutoplayRecommendationsResponse {
  seedTrackIds: number[];
  excludedTrackIds: number[];
  tracks: RecommendationTrackDto[];
}

@Injectable({providedIn: 'root'})
export class AutoplayRecommendationsEndpointService {
  private readonly url = `${MyConfig.api_address}/api/v2/recommendations/autoplay`;

  constructor(private httpClient: HttpClient) {}

  handleAsync(request: AutoplayRecommendationsRequest): Observable<AutoplayRecommendationsResponse> {
    return this.httpClient.post<AutoplayRecommendationsResponse>(this.url, request);
  }
}
