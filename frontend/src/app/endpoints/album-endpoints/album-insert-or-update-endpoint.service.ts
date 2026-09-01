import { Injectable } from '@angular/core';
import {MyBaseEndpointAsync} from '../../helper/my-base-endpoint-async.interface';
import { Observable } from 'rxjs';
import {HttpClient} from '@angular/common/http';
import {MyAuthService} from '../../services/auth-services/my-auth.service';
import {MyConfig} from '../../my-config';

export interface AlbumInsertRequest {
  id?: number;
  title: string;
  distributor: string;
  releaseDate: string;
  albumTypeId: number;
  isActive: boolean;
  coverImage?: File;
  artistId: number;
}

export interface AlbumInsertResponse {
  id: number;
  title: string;
  releaseDate: string;
  isActive: boolean;
  artist: string;
}

@Injectable({
  providedIn: 'root'
})

export class AlbumInsertOrUpdateEndpointService implements MyBaseEndpointAsync<AlbumInsertRequest,AlbumInsertResponse> {
  readonly url = `${MyConfig.api_address}/api/AlbumInsertOrUpdateEndpoint`;
  constructor(private httpClient: HttpClient, private myAuthService: MyAuthService) {
  }

  handleAsync(request: AlbumInsertRequest): Observable<AlbumInsertResponse> {
        let formData = new FormData();
        formData.append("id", request.id?.toString() ?? "");
        formData.append("title", request.title);
        formData.append("distributor", request.distributor);
        formData.append("releaseDate", request.releaseDate);
        if(request.coverImage !== undefined) {
          formData.append("coverImage", request.coverImage);
        }
        formData.append("isActive", request.isActive ? "true" : "false");
        formData.append("albumTypeId",request.albumTypeId.toString());
        formData.append("artistId",request.artistId.toString());

        return this.httpClient.post<AlbumInsertResponse>(this.url,formData);
    }
}
