import {Component, OnInit} from '@angular/core';
import {MyConfig} from '../../../my-config';
import {Params, Router} from '@angular/router';
import {MusicPlayerService} from '../../../services/music-player.service';
import {MyPagedList} from '../../../services/auth-services/dto/my-paged-list';
import {
  AlbumGetAllEndpointService,
  AlbumGetAllResponse, AlbumPagedRequest
} from '../../../endpoints/album-endpoints/album-get-all-endpoint.service';
import {
  ArtistGetAutocompleteEndpointService, UserArtistSearchRequest
} from '../../../endpoints/artist-endpoints/artist-get-autocomplete-endpoint.service';
import {ArtistSimpleDto} from '../../../services/auth-services/dto/artist-dto';
import {
  EventGetUpcomingService,
  UpcomingEvent
} from '../../../endpoints/user-artist-endpoints/event-get-upociming.service';
import {animate, style, transition, trigger} from '@angular/animations';
import {
  HomeRecommendationsEndpointService,
  HomeRecommendationsResponse
} from '../../../endpoints/personalization-endpoints/home-recommendations-endpoint.service';
import {RecommendationTrackMapper} from '../../../services/personalization/recommendation-track.mapper';
import {PlaylistResponse} from '../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {TrackGetResponse} from '../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';

@Component({
  selector: 'app-listener-home',
  templateUrl: './listener-home.component.html',
  styleUrls: ['../artist-page/artist-music-page/artist-music-page.component.css','./listener-home.component.css'],
  animations: [
    trigger('pageAnimation', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('0.4s ease-out', style({ opacity: 1 }))
      ]),
      transition(':leave', [
        style({ opacity: 1 }),
        animate('0.5s ease-in', style({ opacity: 0 }))
      ])
    ]),
    trigger('profileImageAnimation', [
      transition(':enter', [
        style({ transform: 'scale(0)', opacity: 0 }),
        animate('0.3s ease-out', style({ transform: 'scale(1)', opacity: 1 }))
      ])
    ])
  ]})
export class ListenerHomeComponent implements OnInit {
  userId : number = 0;
  protected readonly MyConfig = MyConfig;
  popularAlbums: MyPagedList<AlbumGetAllResponse> | null = null;
  popularParams: Params = {
    title: "Popular Releases",
    popular: "yes"
  };
  recentAlbums: MyPagedList<AlbumGetAllResponse> | null = null;
  recentParams: Params = {
    title: "Recent Releases",
    popular: "no"
  };
  popularArtists: ArtistSimpleDto[] | null = null;
  popArtistParams: Params = {
    title: "Popular Artists",
    popular: "yes",
    needsToHaveSongs: "yes"
  };
  mostStreamedArtists:  ArtistSimpleDto[] | null = null;
  mostStreamedArtistParams: Params = {
    title: "Most Streamed Artists",
    streams: "yes",
    needsToHaveSongs: "yes"
  }
  private slideInterval: any;
  events : UpcomingEvent [] = [];
  infinitePage = [1];
  currentSlide: number = 0;
  homeRecommendations: HomeRecommendationsResponse | null = null;
  homeRecommendationsLoading = true;
  recommendedAlbums: MyPagedList<AlbumGetAllResponse> | null = null;
  recommendedAlbumSubtitles: Record<number, string> = {};
  recommendedArtists: ArtistSimpleDto[] | null = null;
  recommendedArtistDescriptions: Record<number, string> = {};
  recommendedPlaylists: PlaylistResponse[] | null = null;
  recommendedTracks: TrackGetResponse[] = [];
  constructor(private router: Router,
              private musicPlayerService: MusicPlayerService,
              private albumGetService: AlbumGetAllEndpointService,
              private artistGetService: ArtistGetAutocompleteEndpointService,
              private eventGetUpcoming : EventGetUpcomingService,
              private homeRecommendationsEndpoint: HomeRecommendationsEndpointService,
              private recommendationTrackMapper: RecommendationTrackMapper) {
  }

  ngOnInit(): void {
    this.loadEvents();
    this.loadHomeRecommendations();
    this.userId = this.getUserIdFromToken();
    let request: AlbumPagedRequest  = {pageNumber: 1, pageSize: 50, isReleased: true, title: ""};
      this.albumGetService.handleAsync(request).subscribe({
        next: data => {
          this.recentAlbums = data;
        }
      })
    this.albumGetService.handleAsync({...request, sortByPopularity:true}).subscribe({
      next: data => {
          this.popularAlbums = data;
      }
    })

    this.artistGetService.handleAsync({sortByPopularity: true, returnAmount: 6, searchString:"", needsToHaveSongs:true}).subscribe({
      next: data => {
        this.popularArtists = data;
      }
    })

    this.artistGetService.handleAsync({sortByStreams: true, returnAmount: 6, searchString:"", needsToHaveSongs:true}).subscribe({
      next: data => {
        this.mostStreamedArtists = data;
      }
    })
    this.startAutoSlide();
  }

  loadHomeRecommendations(): void {
    this.homeRecommendationsLoading = true;
    this.homeRecommendationsEndpoint.handleAsync().subscribe({
      next: response => {
        this.applyRecommendationCards(response);
        this.homeRecommendations = response;
        this.homeRecommendationsLoading = false;
      },
      error: error => {
        console.warn('Personalized home recommendations are unavailable.', error);
        this.homeRecommendationsLoading = false;
      }
    });
  }

  openDailyPlaylist(id: string): void {
    this.router.navigate(['/listener/playlist/daily', id]);
  }

  playRecommendedTracks(startIndex = 0): void {
    const recommendations = this.homeRecommendations?.recommendedTracks ?? [];
    if(recommendations.length === 0)
    {
      return;
    }

    const tracks = this.recommendationTrackMapper.toPlayerTracks(recommendations);
    const orderedTracks = [...tracks.slice(startIndex), ...tracks.slice(0, startIndex)];
    this.musicPlayerService.createQueue(
      orderedTracks,
      {display: 'Recommended for you', value: '/listener/home'},
      'recommendations');
  }

  mediaUrl(path: string): string {
    const normalizedPath = this.normalizeMediaPath(path);
    if(/^https?:\/\//i.test(normalizedPath))
    {
      return normalizedPath;
    }

    return `${MyConfig.api_address}${normalizedPath}`;
  }

  private applyRecommendationCards(response: HomeRecommendationsResponse): void {
    const albums = response.recommendedAlbums.map(album => ({
      id: album.albumId,
      title: album.title,
      coverArt: this.normalizeMediaPath(album.coverPath),
      releaseDate: response.recommendationDate,
      artist: album.artistName,
      artistId: album.artistId,
      type: 'Album',
      trackCount: album.trackCount,
      isHighlighted: false
    }));
    this.recommendedAlbums = this.toPagedList(albums);
    this.recommendedAlbumSubtitles = response.recommendedAlbums.reduce<Record<number, string>>((descriptions, album) => {
      descriptions[album.albumId] = album.reason;
      return descriptions;
    }, {});

    this.recommendedArtists = response.recommendedArtists.map(artist => ({
      id: artist.artistId,
      name: artist.name,
      pfpPath: this.normalizeMediaPath(artist.profilePhotoPath, '/media/Images/ArtistPfps/placeholder.png'),
      role: '',
      isFlaggedForDeletion: false,
      deletionDate: ''
    }));
    this.recommendedArtistDescriptions = response.recommendedArtists.reduce<Record<number, string>>((descriptions, artist) => {
      descriptions[artist.artistId] = artist.reason;
      return descriptions;
    }, {});

    this.recommendedPlaylists = response.recommendedPlaylists.map(playlist => ({
      id: playlist.playlistId,
      title: playlist.title,
      numOfTracks: playlist.trackCount,
      isPublic: playlist.isPublic,
      coverPath: this.normalizeMediaPath(playlist.coverPath),
      username: playlist.ownerUsername ?? '808 Music',
      isLikedSongs: false,
      userId: playlist.ownerUserId ?? 0,
      ownerUsername: playlist.ownerUsername ?? '808 Music',
      isCollaborative: playlist.isCollaborative,
      description: playlist.reason
    }));
    this.recommendedTracks = this.recommendationTrackMapper.toPlayerTracks(response.recommendedTracks);
  }

  private toPagedList<T>(dataItems: T[]): MyPagedList<T> {
    return {
      dataItems,
      currentPage: 1,
      totalPages: 1,
      pageSize: dataItems.length,
      totalCount: dataItems.length,
      hasPrevious: false,
      hasNext: false
    };
  }

  private normalizeMediaPath(path: string, fallback = '/media/Images/playlist_placeholder.png'): string {
    if(!path)
    {
      return fallback;
    }

    if(/^https?:\/\//i.test(path) || path.startsWith('/media/'))
    {
      return path;
    }

    return `/media/${path.replace(/^\/+/, '')}`;
  }
  ngOnDestroy(): void {
    clearInterval(this.slideInterval);
  }
  loadEvents(): void {
    this.eventGetUpcoming.getUpcomingEvents().subscribe({
      next: (data) => {
        this.events = Array.isArray(data) ? data.filter(Boolean) : [];
        this.currentSlide = 0;
        console.log(this.events);
      },
      error: (err) => {
        console.error('Error fetching events:', err);
      }
    });
  }

  nextSlide(): void {
    if (this.events.length > 0) {
      this.currentSlide = (this.currentSlide + 1) % this.events.length;
    }
  }

  prevSlide(): void {
    if (this.events.length > 0) {
      this.currentSlide = (this.currentSlide - 1 + this.events.length) % this.events.length;
    }
  }
  private startAutoSlide(): void {
    this.slideInterval = setInterval(() => {
      this.nextSlide();
    }, 10000);
  }
  changeSlide(index: number): void {
    this.currentSlide = index;
  }
  private getUserIdFromToken(): number {
    let authToken = sessionStorage.getItem('authToken');

    if (!authToken) {
      authToken = localStorage.getItem('authToken');
    }

    if (!authToken) {
      return 0;
    }

    try {
      const parsedToken = JSON.parse(authToken);
      return parsedToken.userId;
    } catch (error) {
      console.error('Error parsing authToken:', error);
      return 0;
    }
  }

  loadMore() {
    this.infinitePage.push(this.infinitePage[this.infinitePage.length-1]+1);
    console.log("Scrolled")
  }
}
