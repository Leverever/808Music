import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MyConfig } from '../../my-config';

export interface UserSubscriptionDetailsResponse {
  subscription: {
    subscriptionType: number;
    title: string;
    description: string;
    price: number;
    startDate: string;
    endDate: string;
    renewalOn: boolean;
    message: string;
  } | null;
  message?: string;

}

@Injectable({
  providedIn: 'root',
})
export class UserSubscriptionService {
  private apiUrl = `${MyConfig.api_address}/api/UserSubscriptionTypeEndpoint`;

  constructor(private httpClient: HttpClient) {}

  getUserSubscriptionDetails(userId: number): Observable<UserSubscriptionDetailsResponse> {
    return this.httpClient.get<UserSubscriptionDetailsResponse>(`${this.apiUrl}/${userId}`);
  }
}
