import {Injectable} from '@angular/core';
import {RecommendationTrackDto} from '../../endpoints/personalization-endpoints/recommendation.models';
import {TrackGetResponse} from '../../endpoints/track-endpoints/track-get-by-id-endpoint.service';

@Injectable({providedIn: 'root'})
export class RecommendationTrackMapper {
  toPlayerTrack(track: RecommendationTrackDto): TrackGetResponse {
    return {
      id: track.trackId,
      title: track.title,
      length: track.length,
      streams: track.streams,
      isExplicit: track.isExplicit,
      coverPath: this.normalizeMediaPath(track.coverPath),
      trackUserInfo: [],
      artists: track.artists.map(artist => ({
        id: artist.artistId,
        name: artist.name,
        pfpPath: this.normalizeMediaPath(artist.profilePhotoPath),
        isLead: artist.isLead
      })),
      albumId: track.albumId
    };
  }

  toPlayerTracks(tracks: RecommendationTrackDto[]): TrackGetResponse[] {
    return tracks.map(track => this.toPlayerTrack(track));
  }

  private normalizeMediaPath(path: string): string {
    if (!path) {
      return '/media/Images/playlist_placeholder.png';
    }

    if (/^https?:\/\//i.test(path) || path.startsWith('/media/')) {
      return path;
    }

    return `/media/${path.replace(/^\/+/, '')}`;
  }
}
