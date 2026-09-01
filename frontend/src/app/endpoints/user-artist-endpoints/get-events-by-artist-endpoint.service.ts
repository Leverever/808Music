import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { MyConfig } from '../../my-config';

export interface ArtistEvents {
  id: number;
  city: string;
  country: string;
  eventDate: string;
  venue: string;
  eventCover?: string;
  eventTitle : string;
  latitude : number,
  longitude : number;
}

@Injectable({
  providedIn: 'root'
})
export class EventGetByArtistIdService {
  private readonly apiUrl = `${MyConfig.api_address}/api/EventGetByArtistEndpoint/api/EventGetByArtist`;

  constructor(private http: HttpClient) {}
ngOnInit() {}
  getEventsByArtist(artistId: number): Observable<ArtistEvents[]> {
    return this.http.get<ArtistEvents[]>(`${this.apiUrl}/${artistId}`);
  }
}
