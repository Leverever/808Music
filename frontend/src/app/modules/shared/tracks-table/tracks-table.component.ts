import {
  AfterViewInit, ChangeDetectorRef,
  Component,
  EventEmitter,
  inject, input,
  Input,
  OnChanges, OnDestroy,
  OnInit,
  Output,
  SimpleChanges,
  ViewChild
} from '@angular/core';
import {MyConfig} from '../../../my-config';
import {
  ArtistTrackDto,
  TrackGetByIdEndpointService,
  TrackGetResponse, TrackUserInfoDto
} from '../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {TrackWithPositionDto} from '../../../services/auth-services/dto/TrackWithPositionDto';
import {ArtistSimpleDto} from '../../../services/auth-services/dto/artist-dto';
import {MatSort, MatSortModule, Sort} from '@angular/material/sort';
import {TrackDeleteEndpointService} from '../../../endpoints/track-endpoints/track-delete-endpoint.service';
import {MatDialog} from '@angular/material/dialog';
import {MatSnackBar} from '@angular/material/snack-bar';
import {ConfirmDialogComponent} from '../dialogs/confirm-dialog/confirm-dialog.component';
import {
  TrackGetAllEndpointService,
  TrackGetAllRequest
} from '../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {MyPagedList} from '../../../services/auth-services/dto/my-paged-list';
import {PageEvent} from '@angular/material/paginator';
import {MatBottomSheet} from '@angular/material/bottom-sheet';
import {ShareBottomSheetComponent} from '../bottom-sheets/share-bottom-sheet/share-bottom-sheet.component';
import {MusicPlayerService} from '../../../services/music-player.service';
import {AddTrackToLikedSongsService} from '../../../endpoints/playlist-endpoints/add-to-liked-songs-endpoint';
import {TrackInteractionService} from '../../../services/personalization/track-interaction.service';
import {IsLikedSongService} from '../../../endpoints/playlist-endpoints/is-liked-song-endpoint.service';
import {
  GetPlaylistsByUserIdEndpointService
} from '../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {
  PlaylistUpdateTracksRequest,
  PlaylistUpdateTracksService
} from '../../../endpoints/playlist-endpoints/add-track-to-playlist-endpoint.service';
import {PlaylistResponse} from '../../../endpoints/playlist-endpoints/get-all-playlists-endpoint.service';
import {
  RemoveTrackFromPlaylistService
} from '../../../endpoints/playlist-endpoints/delete-track-from-playlist-endpoint.service';
import {HttpErrorResponse} from '@angular/common/http';
import {
  PlaylistTracksGetEndpointService,
  PlaylistTracksGetRequest
} from '../../../endpoints/playlist-endpoints/playlist-get-tracks-endpoint.service';
import {IsOnPlaylistService} from '../../../endpoints/playlist-endpoints/is-song-on-playlist-endpoint.service';
import {ArtistHandlerService} from '../../../services/artist-handler.service';
import {Subscription} from 'rxjs';
import {Router} from '@angular/router';
import {AnimationOptions} from 'ngx-lottie';
import {CdkDragDrop, moveItemInArray, transferArrayItem} from '@angular/cdk/drag-drop';
import {
  ReleaseTracksEndpointService
} from '../../../endpoints/release-track-endpoints/release-tracks-endpoint.service';

interface TrackDiscGroup {
  discNumber: number;
  tracks: TrackWithPositionDto[];
  dropListId: string;
}

type TracksSearchMode = 'tracks' | 'playlist' | 'client';

@Component({
  selector: 'app-tracks-table',
  templateUrl: './tracks-table.component.html',
  styleUrl: './tracks-table.component.css',
})
export class TracksTableComponent implements OnInit, OnChanges, AfterViewInit, OnDestroy {
  @Input() inArtistMode = true;
  @Input() listenerDetailList = false;
  @Input() showPopularityStats = false;
  @Input() isPlaylist = false;
  @Input() searchMode: TracksSearchMode = 'tracks';
  @Input() trackInfo: TrackUserInfoDto [] = [];
  @Input() playlistId: number | null = null;
  @Input() isUsersPlaylist = false;
  @Input() isCollaborative = false;

  protected readonly MyConfig = MyConfig;
  @Input() tracks: TrackGetResponse[] = [];
  tracksDto: TrackWithPositionDto[] = []
  displayedColumns = ["position", "main-control", "title", "artist-controls", "duration", "streams", "add-to-playlist-controls"];
  dataSource = new MatTableDataSource<TrackWithPositionDto>(this.tracksDto);
  @Output() onMainClick: EventEmitter<number> = new EventEmitter();
  matDialog: MatDialog = inject(MatDialog);
  snackBar: MatSnackBar = inject(MatSnackBar);
  playlists: PlaylistResponse[] = [];
  showPlaylistDropdown: boolean = false;
  selectedTrackId: number | null = null;
  pagedResponse: MyPagedList<TrackGetResponse> | null = null;
  @Input() pagedRequest: TrackGetAllRequest = {
    pageNumber: 1,
    pageSize: 10,
  }
  @Output() onCreateClick: EventEmitter<void> = new EventEmitter();
  paginationOptions = [10, 20, 35, 50]
  @Input() reload = true;
  shouldDisplayControls = false;
  isShuffled = false;
  @Input() allowPagination = true;
  @Input() groupAlbumDiscs = false;
  @Input() manageAlbumOrder = false;

  discGroups: TrackDiscGroup[] = [];
  isSavingOrder = false;
  reorderUnavailable = false;

  @ViewChild(MatSort) sort!: MatSort;
  playlistTrackMap: Map<number, Map<number, boolean>> = new Map();

  artist: ArtistSimpleDto | null = null;
  showAnim = true;

  options:AnimationOptions = {
    loop:true,
    path: "/assets/animations/playing_anim.json"
  }

  isPlayingThisAlbum: boolean = false;
  playingState: boolean = false;

  state$!: Subscription;
  trackChange$!: Subscription;

  currentTrack : TrackGetResponse | null = null;
  private searchQuery = '';
  private playlistSearchVersion = 0;

  ngOnDestroy(): void {
    //this.state$.unsubscribe();
    //this.trackChange$.unsubscribe();
  }

  constructor(private getTrackService: TrackGetByIdEndpointService,
              private deleteTrackService: TrackDeleteEndpointService,
              private getAllTracksService: TrackGetAllEndpointService,
              private btmSheet: MatBottomSheet,
              protected musicPlayerService: MusicPlayerService,
              private addTrackToLikedSongsService: AddTrackToLikedSongsService,
              private isLikedSongService: IsLikedSongService,
              private getPlaylistsService: GetPlaylistsByUserIdEndpointService,
              private playlistUpdateTracksService: PlaylistUpdateTracksService,
              private removeTrackFromPlaylistService: RemoveTrackFromPlaylistService,
              private playlistTracksService: PlaylistTracksGetEndpointService,
              private isOnPlaylist: IsOnPlaylistService,
              private removeFromPlaylist: RemoveTrackFromPlaylistService,
              private interactions: TrackInteractionService,
              private artistHandler: ArtistHandlerService,
              private cdRef: ChangeDetectorRef,
              private releaseTracksService: ReleaseTracksEndpointService,
  ) {

  }

  likedSongs: Map<number, boolean> = new Map();
  showDeleteIcon: boolean = true;

  ngAfterViewInit(): void {
    /*
    this.dataSource.sortingDataAccessor = (item, prop) => {
      switch (prop)
      {
        case 'position': console.log(item[prop]); return item[prop];
      }
      return '';
    }

     */
    this.dataSource.sort = this.sort;

    if (this.inArtistMode) {
      this.artist = this.artistHandler.getSelectedArtist();
    }
    this.configureDisplayedColumns();
    this.cdRef.detectChanges();
  }

  ngOnChanges(changes: SimpleChanges): void {
    this.configureDisplayedColumns();

    if (this.searchMode === 'client' && changes['tracks']) {
      this.applyClientSideSearch();
    } else if (this.searchMode === 'playlist' && changes['tracks'] && !this.searchQuery) {
      this.setDisplayedTracks(this.tracks);
    }

    if (this.searchMode === 'playlist' && changes['playlistId'] && this.searchQuery) {
      this.searchPlaylistTracks();
    }

    if (this.searchMode === 'tracks' && this.reload) {
      console.log("changes");
      this.reloadData();
    }
  }

  get showAlbumOrderControls(): boolean {
    return this.manageAlbumOrder && this.artist != null && this.artist.role !== "Viewer";
  }

  get canReorder(): boolean {
    return this.showAlbumOrderControls
      && !this.isSavingOrder
      && !this.reorderUnavailable
      && !this.isOrderFiltered;
  }

  get hasMultipleDiscs(): boolean {
    return this.discGroups.length > 1;
  }

  get discDropListIds(): string[] {
    return this.discGroups.map(group => group.dropListId);
  }

  get tableDiscGroups(): TrackDiscGroup[] {
    if (this.groupAlbumDiscs) {
      return this.discGroups;
    }
    return [{discNumber: 1, tracks: this.tracksDto, dropListId: "tracks-table"}];
  }

  get isOrderFiltered(): boolean {
    return !!this.pagedRequest.title?.trim();
  }

  get hasEmptyDisc(): boolean {
    return this.discGroups.some(group => group.tracks.length === 0);
  }

  get orderHelpText(): string {
    if (this.isSavingOrder) {
      return "Saving track order...";
    }
    if (this.reorderUnavailable) {
      return "This release is too large to reorder in this view.";
    }
    if (this.isOrderFiltered) {
      return "Clear the search to reorder tracks.";
    }
    return "Drag tracks to change their position or move them between discs.";
  }

  addDisc(): void {
    if (!this.canReorder || this.discGroups.some(group => group.tracks.length === 0)) {
      return;
    }

    const discNumber = Math.max(0, ...this.discGroups.map(group => group.discNumber)) + 1;
    this.discGroups = [
      ...this.discGroups,
      {
        discNumber,
        tracks: [],
        dropListId: `album-disc-${discNumber}`
      }
    ];
  }

  removeEmptyDisc(discNumber: number): void {
    const group = this.discGroups.find(candidate => candidate.discNumber === discNumber);
    if (!group || group.tracks.length > 0 || this.isSavingOrder) {
      return;
    }
    this.discGroups = this.discGroups.filter(candidate => candidate !== group);
  }

  dropTrack(event: CdkDragDrop<TrackWithPositionDto[]>): void {
    if (!this.canReorder || event.previousIndex === event.currentIndex
      && event.previousContainer === event.container) {
      return;
    }

    const previousGroups = this.discGroups.map(group => ({
      ...group,
      tracks: group.tracks.map(track => ({...track}))
    }));

    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
    } else {
      transferArrayItem(
        event.previousContainer.data,
        event.container.data,
        event.previousIndex,
        event.currentIndex
      );
    }

    this.discGroups = this.discGroups.filter(group => group.tracks.length > 0);
    this.normalizeDiscPositions();
    this.persistTrackOrder(previousGroups);
  }

  private configureDisplayedColumns(): void {
    const columns = ["position", "main-control", "title"];
    if (this.isCollaborative) {
      columns.push("addedby");
    }
    columns.push("artist-controls", "duration", "streams", "add-to-playlist-controls");
    if (this.manageAlbumOrder && this.artist != null && this.artist.role !== "Viewer") {
      columns.unshift("reorder");
    }
    this.displayedColumns = columns;
  }

  private rebuildDiscGroups(): void {
    if (!this.groupAlbumDiscs) {
      this.discGroups = [];
      return;
    }

    const groups = new Map<number, TrackWithPositionDto[]>();
    for (const track of this.tracksDto) {
      const discNumber = track.discNumber ?? 1;
      const group = groups.get(discNumber) ?? [];
      group.push(track);
      groups.set(discNumber, group);
    }

    if (groups.size === 0) {
      groups.set(1, []);
    }

    this.discGroups = [...groups.entries()]
      .sort(([firstDisc], [secondDisc]) => firstDisc - secondDisc)
      .map(([discNumber, tracks]) => ({
        discNumber,
        tracks: tracks
          .sort((first, second) =>
            (first.trackNumber ?? first.position) - (second.trackNumber ?? second.position))
          .map((track, index) => ({...track, position: index + 1})),
        dropListId: `album-disc-${discNumber}`
      }));
  }

  private normalizeDiscPositions(): void {
    this.discGroups = this.discGroups.map((group, discIndex) => {
      const discNumber = discIndex + 1;
      return {
        discNumber,
        dropListId: `album-disc-${discNumber}`,
        tracks: group.tracks.map((track, trackIndex) => ({
          ...track,
          discNumber,
          trackNumber: trackIndex + 1,
          position: trackIndex + 1
        }))
      };
    });
    this.tracksDto = this.discGroups.flatMap(group => group.tracks);
    this.tracks = this.tracksDto;
    this.dataSource.data = this.tracksDto;
  }

  private persistTrackOrder(previousGroups: TrackDiscGroup[]): void {
    const releaseId = this.pagedRequest.albumId;
    if (releaseId == null) {
      this.restoreDiscGroups(previousGroups);
      this.snackBar.open("The release could not be identified.", "Dismiss", {duration: 3500});
      return;
    }

    this.isSavingOrder = true;
    this.releaseTracksService.reorder(releaseId, {
      tracks: this.discGroups.flatMap(group => group.tracks.map(track => ({
        trackId: track.id,
        discNumber: group.discNumber,
        trackNumber: track.trackNumber ?? track.position
      })))
    }).subscribe({
      next: () => {
        this.isSavingOrder = false;
        this.reloadData();
        this.snackBar.open("Track order saved.", "Dismiss", {duration: 2500});
      },
      error: (error: HttpErrorResponse) => {
        this.isSavingOrder = false;
        this.restoreDiscGroups(previousGroups);
        const message = typeof error.error === "string" && error.error.trim()
          ? error.error
          : "Track order could not be saved. Your previous order has been restored.";
        this.snackBar.open(message, "Dismiss", {duration: 5000});
      }
    });
  }

  private restoreDiscGroups(groups: TrackDiscGroup[]): void {
    this.discGroups = groups;
    this.tracksDto = groups.flatMap(group => group.tracks);
    this.tracks = this.tracksDto;
    this.dataSource.data = this.tracksDto;
  }

  getLikeIcon(id: number): string {
    return this.likedSongs.get(id) ? 'favorite' : 'favorite_border';
  }

  reloadData() {
    this.reload = false;

    if (this.searchMode === 'client') {
      this.applyClientSideSearch();
      return;
    }

    if (this.searchMode === 'playlist') {
      if (this.searchQuery) {
        this.searchPlaylistTracks();
      } else {
        this.setDisplayedTracks(this.tracks);
      }
      return;
    }

    const request = this.groupAlbumDiscs
      ? {...this.pagedRequest, pageNumber: 1, pageSize: 500}
      : this.pagedRequest;

    this.getAllTracksService.handleAsync(request).subscribe({
      next: data => {

        this.pagedResponse = data;
        if (!this.isPlaylist) {
          this.tracks = data.dataItems;
        }
        this.setDisplayedTracks(this.tracks, true);
        this.reorderUnavailable = this.manageAlbumOrder && data.totalCount > this.tracksDto.length;
      },
      error: error => {
        console.error('Error reloading data:', error);
      },
    });
  }

  initializePlaylistCheckboxes(trackId: number): void {
    if (!this.playlists.length) return;

    this.playlists.forEach(playlist => {
      const request = {playlistId: playlist.id, trackId: trackId};
      console.log("checkbox", request);
      this.isOnPlaylist.handleAsync(request).subscribe({
        next: (response) => {
          if (!this.playlistTrackMap.has(playlist.id)) {
            this.playlistTrackMap.set(playlist.id, new Map());
          }
          this.playlistTrackMap.get(playlist.id)!.set(trackId, response.isAlreadyOnPlaylist);
        },
        error: (error) => {
          console.error(`Error checking track ${trackId} on playlist ${playlist.id}:`, error);
        },
      });
    });
  }

  ngOnInit(): void {
    console.log('u track table veli', this.isCollaborative);
    this.reloadData();
    this.loadPlaylists();

    this.playingState = this.musicPlayerService.getPlayState();
    this.currentTrack = this.musicPlayerService.getLastPlayedSong();

    this.state$ = this.musicPlayerService.playStateChange.subscribe(state => this.playingState = state);
    this.trackChange$ = this.musicPlayerService.trackEvent.subscribe(track => {
      this.currentTrack = track;
      this.isPlayingThisAlbum = this.tracks.length > 0 && (
        this.musicPlayerService.getLastPlayedSong()?.albumId == this.tracks[0].albumId && this.musicPlayerService.getQueueType() === "album"
        || this.musicPlayerService.getLastPlayedSong()?.albumId == this.playlistId && this.musicPlayerService.getQueueType() === "playlist"
      )});

    this.musicPlayerService.shuffleToggled.subscribe({
      next: data => {
        this.isShuffled = data;
      }
    })
  }

  getPosition(id: number) {
    const track = this.tracksDto.find(x => x.id === id);
    return track?.position.toString();
  }

  getArtists(id: number) {
    let track = this.tracksDto.find(x => x.id === id)!;
    let artists = "";
    for (let i = 0; i < track.artists.length; i++) {
      artists += i == 0 ? track.artists[i].name : ', ' + track.artists[i].name;
    }
    return artists;
  }

  loadPlaylists() {
    const userId = this.getUserIdFromToken();
    if (userId) {
      this.getPlaylistsService.handleAsync(userId).subscribe({
        next: (playlists) => {
          this.playlists = playlists;

          console.log(this.playlists);

        },
        error: (error) => {
          console.error('Error loading playlists:', error);
        },
      });
    }
  }

  toggleDropdown(trackId: number) {
    this.selectedTrackId = trackId;
    this.initializePlaylistCheckboxes(trackId);
    this.showPlaylistDropdown = !this.showPlaylistDropdown;

    console.log(this.showPlaylistDropdown);

  }

  getDuration(id: number) {
    let track = this.tracksDto.find(x => x.id === id)!;
    let minutes = Math.floor(track.length / 60).toString();
    let seconds = (track.length % 60).toFixed(0);

    if (Number(seconds) < 10) {
      seconds = '0' + seconds;
    }
    return `${minutes}:${seconds}`;
  }

  goToArtistProfile(artist: ArtistSimpleDto | ArtistTrackDto) {
    //TODO: Implement when user side profiles are made
    console.log(artist);
  }

  displayControls(b: boolean) {
    this.shouldDisplayControls = b;
    console.log(this.shouldDisplayControls);
  }


  emitTrack(id: number) {
    /*
    this.getTrackService.handleAsync(id).subscribe({
      next: data => {
        this.onMainClick.emit(data);
      }
    })
    */
    this.onMainClick.emit(id);
  }

  playTrackFromMobileRow(event: MouseEvent, track: TrackGetResponse): void {
    if(!window.matchMedia('(max-width: 960px)').matches || this.inArtistMode)
    {
      return;
    }

    const target = event.target as HTMLElement | null;
    if(target?.closest(
      'button, a, input, mat-checkbox, app-clickable-featured-artists, .clickable-artist, .delete-icon'
    ))
    {
      return;
    }

    if(this.currentTrack?.id === track.id)
    {
      if(!this.playingState)
      {
        this.musicPlayerService.togglePlayState();
      }
      return;
    }

    this.emitTrack(track.id);
  }

  deleteTrack(id: number) {
    let matRef = this.matDialog.open(ConfirmDialogComponent, {
      hasBackdrop: true,
      data: {
        title: "Are you sure you want to delete this track",
        content: "This will permanently remove this track from your catalogue!"
      }
    })

    matRef.afterClosed().subscribe({
      next: data => {
        if (data) {
          this.deleteTrackService.handleAsync(id).subscribe({
            next: data => {
              this.snackBar.open(data, "Dismiss", {duration: 3500});
              this.reloadData();
            }
          })
        }
      }
    })
  }

  setPageOpitions(page: PageEvent) {
    this.pagedRequest.pageNumber = page.pageIndex + 1;
    this.pagedRequest.pageSize = page.pageSize;
    this.reloadData();
  }

  searchTracks(queryString: string | null | undefined) {
    this.playlistSearchVersion++;
    this.searchQuery = (queryString ?? '').trim();
    this.pagedRequest.title = this.searchQuery;
    this.pagedRequest.pageNumber = 1;
    this.reloadData();
  }

  private applyClientSideSearch(): void {
    this.playlistSearchVersion++;
    const query = this.normalizeSearchValue(this.searchQuery);
    const filteredTracks = query
      ? this.tracks.filter(track => {
        const titleMatches = this.normalizeSearchValue(track.title).includes(query);
        const artistMatches = track.artists.some(artist =>
          this.normalizeSearchValue(artist.name).includes(query));
        return titleMatches || artistMatches;
      })
      : this.tracks;

    this.setDisplayedTracks(filteredTracks);
  }

  private searchPlaylistTracks(): void {
    const playlistId = this.playlistId;
    if (playlistId == null) {
      this.setDisplayedTracks(this.tracks);
      return;
    }

    const searchVersion = ++this.playlistSearchVersion;
    const request: PlaylistTracksGetRequest = {
      playlistId,
      pageNumber: 1,
      pageSize: this.pagedRequest.pageSize || 50,
      title: this.searchQuery
    };

    this.playlistTracksService.handleAsync(request).subscribe({
      next: response => {
        if (searchVersion !== this.playlistSearchVersion) {
          return;
        }
        this.pagedResponse = response;
        this.setDisplayedTracks(response.dataItems || []);
      },
      error: (error: HttpErrorResponse) => {
        if (searchVersion === this.playlistSearchVersion) {
          console.error('Error searching playlist tracks:', error);
        }
      }
    });
  }

  private normalizeSearchValue(value: string): string {
    return value.trim().toLocaleLowerCase();
  }

  private setDisplayedTracks(tracks: TrackGetResponse[], resetLikes = false): void {
    this.tracksDto = tracks.map((track, index) => ({
      ...track,
      position: track.trackNumber ?? index + 1,
    }));
    this.dataSource.data = this.tracksDto;
    this.rebuildDiscGroups();

    if (resetLikes) {
      this.likedSongs.clear();
    }

    this.tracksDto.forEach(track => {
      if (this.likedSongs.has(track.id)) {
        return;
      }

      const request = {trackId: track.id, userId: this.getUserIdFromToken()};
      this.isLikedSongService.handleAsync(request).subscribe({
        next: response => this.likedSongs.set(track.id, response.isLikedSong),
      });
    });

    this.isPlayingThisAlbum = this.tracks.length > 0 && (
      this.musicPlayerService.getLastPlayedSong()?.albumId == this.tracks[0].albumId
        && this.musicPlayerService.getQueueType() === "album"
      || this.musicPlayerService.getLastPlayedSong()?.albumId == this.playlistId
        && this.musicPlayerService.getQueueType() === "playlist"
    );
  }

  emitCreate() {
    this.onCreateClick.emit();
  }

  openShareSheet() {
    let matRef = this.btmSheet.open(ShareBottomSheetComponent, {
      hasBackdrop: true, data: {
        url: MyConfig.ui_address + "/listener/release/" + this.tracks[0].albumId,
      }
    });

  }

  toggleShuffle() {
    this.musicPlayerService.toggleShuffle();
  }

  sortData(sort: Sort) {
    switch (sort.active) {
      case "position":
        if (sort.direction == 'asc') {
          this.tracksDto.sort((t1, t2) => t1.position - t2.position)
        } else if (sort.direction == 'desc') {
          this.tracksDto.sort((t1, t2) => t2.position - t1.position)
        } else {

        }
        break;
    }
    this.dataSource.data = this.tracksDto;
  }

  addToLikedSongs(id: number) {
    const isLiked = this.likedSongs.get(id) || false;
    const request = {trackId: id, userId: this.getUserIdFromToken()};

    if (isLiked) {
      this.addTrackToLikedSongsService.handleAsync(request).subscribe({
        next: () => {
          this.likedSongs.set(id, false);
          this.interactions.record(id, 'Unliked', {contextType: 'Playback'});
          this.snackBar.open("Song removed from liked songs", "Dismiss", {duration: 3500});
        },
        error: error => {
          console.error('Error removing track:', error);
        },
      });
    } else {
      this.addTrackToLikedSongsService.handleAsync(request).subscribe({
        next: () => {
          this.likedSongs.set(id, true);
          this.interactions.record(id, 'Liked', {contextType: 'Playback'});
          this.snackBar.open("Song added to liked songs", "Dismiss", {duration: 3500});
        },
        error: error => {
          console.error('Error adding track:', error);
        },
      });
    }
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

  removeTrackFromPlaylist(trackId: number): void {
    let matRef = this.matDialog.open(ConfirmDialogComponent, {
      hasBackdrop: true,
      data: {
        title: "Are you sure?",
        content: "This will remove the song from your playlist"
      }
    });

    matRef.afterClosed().subscribe((confirmed: boolean) => {
      if (confirmed) {
        if (this.playlistId) {
          this.removeTrackFromPlaylistService.handleAsync(this.playlistId, trackId).subscribe({
            next: () => {
              console.log(`Track ${trackId} successfully removed from playlist ${this.playlistId}`);
              this.loadPlaylistTracks(this.playlistId!);
            },
            error: (error) => {
              console.error('Error removing track:', error);
            },
          });
        } else {
          console.error('Playlist ID is not defined');
        }
      } else {
        console.log('User canceled the action');
      }
    });
  }

  private loadPlaylistTracks(playlistId: number): void {
    const request = {playlistId, pageNumber: 1, pageSize: 50};
    this.playlistTracksService.handleAsync(request).subscribe({
      next: (response) => {
        this.tracks = response.dataItems || [];
      },
      error: (err: HttpErrorResponse) => {
        console.error('Error fetching playlist tracks:', err);
      },
    });
  }

  addToPlaylist(playlistId: number) {
    if (this.selectedTrackId) {
      const isInPlaylist = this.isTrackInPlaylist(this.selectedTrackId, playlistId);

      if (isInPlaylist) {
        this.removeTrackFromPlaylistService.handleAsync(playlistId, this.selectedTrackId).subscribe({
          next: () => {
            this.snackBar.open('Track removed from playlist!', 'Dismiss', {duration: 3500});
            this.playlistTrackMap.get(playlistId)?.set(this.selectedTrackId!, false);
            this.showPlaylistDropdown = false;
          },
          error: (error) => {
            console.error('Error removing track from playlist:', error);
          },
        });
      } else {
        const request: PlaylistUpdateTracksRequest = {
          playlistId: playlistId,
          userId: this.getUserIdFromToken(),
          trackIds: [this.selectedTrackId],
        };

        this.playlistUpdateTracksService.handleAsync(request).subscribe({
          next: () => {
            this.snackBar.open('Track added to playlist!', 'Dismiss', {duration: 3500});
            this.showPlaylistDropdown = false;
          },
          error: (error) => {
            console.error('Error adding track to playlist:', error);
          },
        });
      }
    }
  }


  isTrackInPlaylist(trackId: number, playlistId: number) {
    return this.playlistTrackMap.get(playlistId)?.get(trackId) ?? false;
  }

  addToQueue(track: TrackGetResponse) {
    this.musicPlayerService.addToQueue(track);
    this.snackBar.open(`${track.title} added to queue.`, "Dismiss", {duration: 2000});
  }

  showHide(b: boolean, track:TrackGetResponse): void {
    if(track.id === this.currentTrack?.id)
    {
      this.showAnim = b;
    }
  }
}
