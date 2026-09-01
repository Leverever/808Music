import {Component, OnInit} from '@angular/core';
import {Location} from '@angular/common';
import {ActivatedRoute, Router} from '@angular/router';
import {MyConfig} from '../../../my-config';
import {
  PersonalizedPlaylistDetail,
  PersonalizedPlaylistsEndpointService
} from '../../../endpoints/personalization-endpoints/personalized-playlists-endpoint.service';
import {TrackGetResponse} from '../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {RecommendationTrackMapper} from '../../../services/personalization/recommendation-track.mapper';
import {MusicPlayerService} from '../../../services/music-player.service';

@Component({
  selector: 'app-personalized-playlist-detail',
  templateUrl: './personalized-playlist-detail.component.html',
  styleUrl: './personalized-playlist-detail.component.css'
})
export class PersonalizedPlaylistDetailComponent implements OnInit {
  playlist: PersonalizedPlaylistDetail | null = null;
  tracks: TrackGetResponse[] = [];
  loading = true;
  errorMessage = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private endpoint: PersonalizedPlaylistsEndpointService,
    private mapper: RecommendationTrackMapper,
    private musicPlayerService: MusicPlayerService,
    private location: Location
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if(!id)
    {
      this.router.navigate(['/listener/playlist']);
      return;
    }

    this.endpoint.getById(id).subscribe({
      next: playlist => {
        this.playlist = playlist;
        this.tracks = this.mapper.toPlayerTracks(playlist.tracks);
        this.loading = false;
      },
      error: error => {
        console.error('Could not load personalized playlist.', error);
        this.errorMessage = 'This daily playlist could not be loaded.';
        this.loading = false;
      }
    });
  }

  playAll(startIndex = 0): void {
    if(this.playlist == null || this.tracks.length === 0)
    {
      return;
    }

    const orderedTracks = [...this.tracks.slice(startIndex), ...this.tracks.slice(0, startIndex)];
    this.musicPlayerService.createQueue(
      orderedTracks,
      {
        display: `${this.playlist.name} - Daily Mix`,
        value: `/listener/playlist/daily/${this.playlist.id}`
      },
      'personalized-playlist');
  }

  playTrack(trackId: number): void {
    const startIndex = this.tracks.findIndex(track => track.id === trackId);
    if(startIndex >= 0)
    {
      this.playAll(startIndex);
    }
  }

  getTotalTrackLength(): string {
    const totalSeconds = this.tracks.reduce((total, track) => total + track.length, 0);
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    return `${minutes}m ${seconds}s`;
  }

  playlistCoverUrl(): string {
    const firstTrack = this.playlist?.tracks[0];
    const firstArtist = firstTrack?.artists.find(artist => artist.isLead) ?? firstTrack?.artists[0];
    return this.mediaUrl(this.playlist?.coverPath || firstArtist?.profilePhotoPath);
  }

  get playlistArtworkCss(): string {
    if(!this.playlist)
    {
      return 'none';
    }

    return `url("${this.playlistCoverUrl()}")`;
  }

  mediaUrl(path?: string): string {
    return MyConfig.mediaUrl(path, 'Images/ArtistPfps/placeholder.png');
  }

  goBack(): void {
    this.location.back();
  }
}
