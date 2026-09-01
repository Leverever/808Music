import {Component, OnInit} from '@angular/core';
import {MatChipSelectionChange} from '@angular/material/chips';
import {ActivatedRoute, Params, Router} from '@angular/router';
import {MatSnackBar} from '@angular/material/snack-bar';
import {catchError, forkJoin, of} from 'rxjs';
import {
  ArtistGetAutocompleteEndpointService,
  UserArtistSearchRequest
} from '../../../endpoints/artist-endpoints/artist-get-autocomplete-endpoint.service';
import {
  AlbumGetAllEndpointService,
  AlbumGetAllResponse,
  AlbumPagedRequest
} from '../../../endpoints/album-endpoints/album-get-all-endpoint.service';
import {
  TrackGetAllEndpointService,
  TrackGetAllRequest,
  TrackGetResponse
} from '../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {
  UserSearchEndpointService,
  UserSearchRequest,
  UserSearchResponse
} from '../../../endpoints/user-endpoints/user-search-endpoint.service';
import {
  GetAllPlaylistsService
} from '../../../endpoints/playlist-endpoints/get-all-playlists-endpoint.service';
import {
  PlaylistResponse
} from '../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {
  HomeRecommendationsEndpointService,
  HomeRecommendationsResponse
} from '../../../endpoints/personalization-endpoints/home-recommendations-endpoint.service';
import {
  PersonalizedPlaylistSummary,
  PersonalizedPlaylistsEndpointService
} from '../../../endpoints/personalization-endpoints/personalized-playlists-endpoint.service';
import {MyPagedList} from '../../../services/auth-services/dto/my-paged-list';
import {ArtistSimpleDto} from '../../../services/auth-services/dto/artist-dto';
import {ArtistHandlerService} from '../../../services/artist-handler.service';
import {RecommendationTrackMapper} from '../../../services/personalization/recommendation-track.mapper';
import {MusicPlayerService} from '../../../services/music-player.service';
import {MyConfig} from '../../../my-config';

type SearchFilter = 'tracks' | 'albums' | 'artists' | 'playlists' | 'users';

@Component({
  selector: 'app-search-page',
  templateUrl: './search-page.component.html',
  styleUrls: ['./search-page.component.css']
})
export class SearchPageComponent implements OnInit {
  artistMode = false;
  isLoading = true;
  recommendationsUnavailable = false;
  allMode = true;
  filter = {
    showAlbums: true,
    showArtists: true,
    showTracks: true,
    showPlaylists: true,
    showUsers: true
  };

  albums: MyPagedList<AlbumGetAllResponse> = this.emptyPagedList<AlbumGetAllResponse>();
  artists: ArtistSimpleDto[] = [];
  tracks: MyPagedList<TrackGetResponse> = this.emptyPagedList<TrackGetResponse>();
  playlists: PlaylistResponse[] = [];
  users: UserSearchResponse[] = [];

  homeRecommendations: HomeRecommendationsResponse | null = null;
  dailyPlaylists: PersonalizedPlaylistSummary[] = [];
  recommendedAlbums: MyPagedList<AlbumGetAllResponse> | null = null;
  recommendedAlbumSubtitles: Record<number, string> = {};
  recommendedArtists: ArtistSimpleDto[] = [];
  recommendedArtistDescriptions: Record<number, string> = {};
  recommendedPlaylists: PlaylistResponse[] = [];
  recommendedPlaylistReasons: Record<number, string> = {};
  recommendedTracks: TrackGetResponse[] = [];

  query = '';
  private searchVersion = 0;

  artistRequest: UserArtistSearchRequest = {
    sortByPopularity: true,
    searchString: '',
    returnAmount: 30
  };

  albumRequest: AlbumPagedRequest = {
    pageNumber: 1,
    pageSize: 30,
    sortByPopularity: true,
    title: '',
    isReleased: true
  };

  trackRequest: TrackGetAllRequest = {
    pageNumber: 1,
    pageSize: 30,
    isReleased: true,
    sortByStreams: true
  };

  popArtistParams: Params = {
    popular: 'yes',
    searchString: ''
  };

  albumParams: Params = {
    title: 'Album search results',
    popular: 'yes',
    albumTitle: ''
  };

  userRequest: UserSearchRequest = {
    searchString: '',
    returnAmount: 12
  };

  constructor(
    private artistGetService: ArtistGetAutocompleteEndpointService,
    private albumGetService: AlbumGetAllEndpointService,
    private trackGetService: TrackGetAllEndpointService,
    private route: ActivatedRoute,
    private artistHandler: ArtistHandlerService,
    private userGetService: UserSearchEndpointService,
    private playlistGetService: GetAllPlaylistsService,
    private homeRecommendationsEndpoint: HomeRecommendationsEndpointService,
    private recommendationTrackMapper: RecommendationTrackMapper,
    private personalizedPlaylistsEndpoint: PersonalizedPlaylistsEndpointService,
    private musicPlayerService: MusicPlayerService,
    private snackBar: MatSnackBar,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.route.data.subscribe(data => {
      this.artistMode = Boolean(data['artist']);
      if(this.artistMode)
      {
        const artist = this.artistHandler.getSelectedArtist();
        this.trackRequest.leadArtistId = artist?.id;
        this.albumRequest.artistId = artist?.id;
        this.albumRequest.isReleased = undefined;
        this.trackRequest.isReleased = undefined;
      }

      this.search('');
    });
  }

  get hasQuery(): boolean {
    return this.query.length > 0;
  }

  get allFiltersSelected(): boolean {
    if(this.artistMode)
    {
      return this.filter.showTracks && this.filter.showAlbums;
    }

    return this.filter.showTracks
      && this.filter.showAlbums
      && this.filter.showArtists
      && this.filter.showPlaylists
      && this.filter.showUsers;
  }

  get hasEnabledFilters(): boolean {
    if(this.artistMode)
    {
      return this.filter.showTracks || this.filter.showAlbums;
    }

    return this.filter.showTracks
      || this.filter.showAlbums
      || this.filter.showArtists
      || this.filter.showPlaylists
      || this.filter.showUsers;
  }

  get hasVisibleResults(): boolean {
    return (this.filter.showTracks && this.tracks.dataItems.length > 0)
      || (this.filter.showAlbums && this.albums.dataItems.length > 0)
      || (!this.artistMode && this.filter.showArtists && this.artists.length > 0)
      || (!this.artistMode && this.filter.showPlaylists && this.playlists.length > 0)
      || (!this.artistMode && this.filter.showUsers && this.users.length > 0);
  }

  get hasRecommendations(): boolean {
    return this.dailyPlaylists.length > 0
      || (this.recommendedAlbums?.dataItems.length ?? 0) > 0
      || this.recommendedArtists.length > 0
      || this.recommendedPlaylists.length > 0
      || this.recommendedTracks.length > 0;
  }

  search(query: string): void {
    const normalizedQuery = (query ?? '').trim();
    this.query = normalizedQuery;
    this.artistRequest.searchString = normalizedQuery;
    this.trackRequest.title = normalizedQuery;
    this.albumRequest.title = normalizedQuery;
    this.userRequest.searchString = normalizedQuery;
    this.popArtistParams['searchString'] = normalizedQuery;
    this.albumParams['albumTitle'] = normalizedQuery;

    const version = ++this.searchVersion;
    if(!this.artistMode && normalizedQuery.length === 0)
    {
      this.clearSearchResults();
      this.loadHomeRecommendations(version);
      return;
    }

    this.isLoading = true;
    this.recommendationsUnavailable = false;
    forkJoin({
      albums: this.albumGetService.handleAsync({...this.albumRequest}).pipe(
        catchError(() => of(this.emptyPagedList<AlbumGetAllResponse>()))
      ),
      tracks: this.trackGetService.handleAsync({...this.trackRequest}).pipe(
        catchError(() => of(this.emptyPagedList<TrackGetResponse>()))
      ),
      artists: this.artistMode
        ? of([] as ArtistSimpleDto[])
        : this.artistGetService.handleAsync({...this.artistRequest}).pipe(
          catchError(() => of([] as ArtistSimpleDto[]))
        ),
      playlists: this.artistMode
        ? of([] as PlaylistResponse[])
        : this.playlistGetService.handleAsync({
          searchString: normalizedQuery,
          returnAmount: 30,
          publicOnly: true
        }).pipe(catchError(() => of([] as PlaylistResponse[]))),
      users: this.artistMode
        ? of([] as UserSearchResponse[])
        : this.userGetService.handleAsync({...this.userRequest}).pipe(
          catchError(() => of([] as UserSearchResponse[]))
        )
    }).subscribe(results => {
      if(version !== this.searchVersion)
      {
        return;
      }

      this.albums = results.albums;
      this.tracks = results.tracks;
      this.artists = results.artists;
      this.playlists = results.playlists;
      this.users = results.users;
      this.isLoading = false;
    });
  }

  selectAll(event: MatChipSelectionChange): void {
    this.allMode = event.selected;

    if(event.selected)
    {
      this.filter.showAlbums = true;
      this.filter.showArtists = true;
      this.filter.showTracks = true;
      this.filter.showPlaylists = true;
      this.filter.showUsers = true;
    }
  }

  flip(filter: SearchFilter): void {
    switch(filter)
    {
      case 'tracks':
        this.filter.showTracks = !this.filter.showTracks;
        break;
      case 'albums':
        this.filter.showAlbums = !this.filter.showAlbums;
        break;
      case 'artists':
        this.filter.showArtists = !this.filter.showArtists;
        break;
      case 'playlists':
        this.filter.showPlaylists = !this.filter.showPlaylists;
        break;
      case 'users':
        this.filter.showUsers = !this.filter.showUsers;
        break;
    }
  }

  refresh(shouldRefresh: boolean): void {
    if(shouldRefresh)
    {
      this.search(this.query);
    }
  }

  openProfile(userId: number): void {
    this.router.navigate(['listener/user/', userId]);
  }

  openDailyPlaylist(id: string): void {
    this.router.navigate(['/listener/playlist/daily', id]);
  }

  startDailyPlaylist(id: string): void {
    this.personalizedPlaylistsEndpoint.getById(id).subscribe({
      next: playlist => {
        const tracks = this.recommendationTrackMapper.toPlayerTracks(playlist.tracks);
        if(tracks.length === 0)
        {
          this.snackBar.open('This daily playlist has no songs yet.', '', {duration: 2000});
          return;
        }

        this.musicPlayerService.createQueue(
          tracks,
          {display: `${playlist.name} - Daily Mix`, value: `/listener/playlist/daily/${playlist.id}`},
          'personalized-playlist'
        );
      },
      error: error => console.error('Could not start daily playlist.', error)
    });
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
      {display: 'Recommended for you', value: '/listener/search'},
      'recommendations'
    );
  }

  mediaUrl(path: string): string {
    const normalizedPath = this.normalizeMediaPath(path);
    return /^https?:\/\//i.test(normalizedPath)
      ? normalizedPath
      : `${MyConfig.api_address}${normalizedPath}`;
  }

  userImageUrl(path?: string): string {
    if(!path)
    {
      return `${MyConfig.media_address}Images/ProfilePictures/placeholder.png`;
    }

    if(/^https?:\/\//i.test(path))
    {
      return path;
    }

    return path.startsWith('/media/')
      ? `${MyConfig.api_address}${path}`
      : `${MyConfig.media_address}${path.replace(/^\/+/, '')}`;
  }

  private loadHomeRecommendations(version: number): void {
    if(this.homeRecommendations)
    {
      this.isLoading = false;
      return;
    }

    this.isLoading = true;
    this.recommendationsUnavailable = false;
    this.homeRecommendationsEndpoint.handleAsync().subscribe({
      next: response => {
        if(version !== this.searchVersion)
        {
          return;
        }

        this.homeRecommendations = response;
        this.applyRecommendationCards(response);
        this.isLoading = false;
      },
      error: error => {
        if(version !== this.searchVersion)
        {
          return;
        }

        console.warn('Personalized search recommendations are unavailable.', error);
        this.recommendationsUnavailable = true;
        this.isLoading = false;
      }
    });
  }

  private applyRecommendationCards(response: HomeRecommendationsResponse): void {
    this.dailyPlaylists = response.dailyPersonalizedPlaylists.map(playlist => ({
      id: playlist.playlistId,
      themeKey: playlist.themeKey,
      name: playlist.name,
      description: playlist.description,
      coverPath: playlist.coverPath,
      playlistDate: playlist.playlistDate,
      createdAt: playlist.createdAt,
      trackCount: playlist.trackCount
    }));

    this.recommendedAlbums = this.toPagedList(response.recommendedAlbums.map(album => ({
      id: album.albumId,
      title: album.title,
      coverArt: this.normalizeMediaPath(album.coverPath),
      releaseDate: response.recommendationDate,
      artist: album.artistName,
      artistId: album.artistId,
      type: 'Album',
      trackCount: album.trackCount,
      isHighlighted: false
    })));
    this.recommendedAlbumSubtitles = response.recommendedAlbums.reduce<Record<number, string>>((items, album) => {
      items[album.albumId] = album.reason;
      return items;
    }, {});

    this.recommendedArtists = response.recommendedArtists.map(artist => ({
      id: artist.artistId,
      name: artist.name,
      pfpPath: this.normalizeMediaPath(artist.profilePhotoPath, '/media/Images/ArtistPfps/placeholder.png'),
      role: '',
      isFlaggedForDeletion: false,
      deletionDate: ''
    }));
    this.recommendedArtistDescriptions = response.recommendedArtists.reduce<Record<number, string>>((items, artist) => {
      items[artist.artistId] = artist.reason;
      return items;
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
    this.recommendedPlaylistReasons = response.recommendedPlaylists.reduce<Record<number, string>>((reasons, playlist) => {
      reasons[playlist.playlistId] = playlist.reason;
      return reasons;
    }, {});
    this.recommendedTracks = this.recommendationTrackMapper.toPlayerTracks(response.recommendedTracks);
  }

  private clearSearchResults(): void {
    this.albums = this.emptyPagedList<AlbumGetAllResponse>();
    this.artists = [];
    this.tracks = this.emptyPagedList<TrackGetResponse>();
    this.playlists = [];
    this.users = [];
  }

  private toPagedList<T>(dataItems: T[]): MyPagedList<T> {
    return {
      dataItems,
      currentPage: 1,
      totalPages: dataItems.length > 0 ? 1 : 0,
      pageSize: dataItems.length,
      totalCount: dataItems.length,
      hasPrevious: false,
      hasNext: false
    };
  }

  private emptyPagedList<T>(): MyPagedList<T> {
    return this.toPagedList<T>([]);
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
}
