import {Component, HostBinding, HostListener, inject, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {
  TrackGetByIdEndpointService,
  TrackGetResponse
} from '../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {MyConfig} from '../../../my-config';
import {AlbumGetByIdEndpointService} from '../../../endpoints/album-endpoints/album-get-by-id-endpoint.service';
import {
  TrackGetAllEndpointService,
  TrackGetAllRequest
} from '../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {ProductAddResponse} from '../../../endpoints/products-endpoints/product-create-endpoint.service';
import {MyPagedList} from '../../../services/auth-services/dto/my-paged-list';
import {MusicPlayerService} from '../../../services/music-player.service';
import {
  MatBottomSheet,
  MatBottomSheetConfig,
  MatBottomSheetRef
} from '@angular/material/bottom-sheet';
import {
  QueueViewBottomSheetComponent
} from '../../shared/bottom-sheets/queue-view-bottom-sheet/queue-view-bottom-sheet.component';
import {ShareBottomSheetComponent} from '../../shared/bottom-sheets/share-bottom-sheet/share-bottom-sheet.component';
import {catchError, forkJoin, of, Subscription} from 'rxjs';
import {NavigationStart, Router} from '@angular/router';
import {
  IsSubscribedRequest,
  IsSubscribedService
} from '../../../endpoints/auth-endpoints/is-subscribed-endpoint.service';
import {MatDialog} from '@angular/material/dialog';
import {PleaseSubscribeComponent} from '../../shared/bottom-sheets/please-subscribe/please-subscribe.component';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';
import {SendSongMessageComponent} from '../../shared/bottom-sheets/send-song-message/send-song-message.component';
import {
  PlaylistUpdateTracksRequest, PlaylistUpdateTracksService
} from '../../../endpoints/playlist-endpoints/add-track-to-playlist-endpoint.service';
import {MatSnackBar} from '@angular/material/snack-bar';
import {PlaylistResponse} from '../../../endpoints/playlist-endpoints/get-all-playlists-endpoint.service';
import {
  GetPlaylistsByUserIdEndpointService
} from '../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {AddTrackToLikedSongsService} from '../../../endpoints/playlist-endpoints/add-to-liked-songs-endpoint';
import {IsLikedSongService} from '../../../endpoints/playlist-endpoints/is-liked-song-endpoint.service';
import {
  RemoveTrackFromPlaylistService
} from '../../../endpoints/playlist-endpoints/delete-track-from-playlist-endpoint.service';
import {IsOnPlaylistService} from '../../../endpoints/playlist-endpoints/is-song-on-playlist-endpoint.service';
import {TrackInteractionService} from '../../../services/personalization/track-interaction.service';
import {MusicControllerComponent} from './music-controller/music-controller.component';
import {
  AddToPlaylistBottomSheetComponent,
  AddToPlaylistBottomSheetResult
} from '../../shared/bottom-sheets/add-to-playlist-bottom-sheet/add-to-playlist-bottom-sheet.component';
import {
  StemMixerBottomSheetComponent
} from './stem-mixer-bottom-sheet/stem-mixer-bottom-sheet.component';
import {
  RecommendationReasonBottomSheetComponent
} from './recommendation-reason-bottom-sheet/recommendation-reason-bottom-sheet.component';

@Component({
  selector: 'app-music-player',
  templateUrl: './music-player.component.html',
  styleUrl: './music-player.component.css',
  standalone: false
})
export class MusicPlayerComponent implements OnInit, OnDestroy {
  @ViewChild('musicController') musicController?: MusicControllerComponent;
  @HostBinding('class.expanded-host') get expandedHost(): boolean {
    return this.isExpanded;
  }
  @HostBinding('class.action-sheet-open') isPlayerActionSheetOpen = false;

  track:TrackGetResponse | null = null;
  playlists: PlaylistResponse[] = [];
  trackId = 0;
  newTrackId: number = 39;
  queueManager = inject(MatBottomSheet)
  isSubbed = true;
  selectedTrackId  = 0;
  likedSongs: Map<number, boolean> = new Map();
  playlistTrackMap: Map<number, Map<number, boolean>> = new Map();
  playingState = false;
  currentPlaybackTime = 0;
  playbackDuration = 0;
  isExpanded = false;
  isMobileViewport = window.matchMedia('(max-width: 960px)').matches;
  swipeOffset = 0;
  swipeAnimating = false;
  swipeSurface: 'mini' | 'cover' | null = null;
  swipeCommitDirection: 'previous' | 'next' | null = null;
  swipePreviousTrack: TrackGetResponse | null = null;
  swipeNextTrack: TrackGetResponse | null = null;
  private subscriptions = new Subscription();
  private swipePointerId: number | null = null;
  private swipeStartX = 0;
  private swipeStartTime = 0;
  private swipeWidth = 1;
  private swipeTimer: ReturnType<typeof setTimeout> | null = null;
  private swipeAnimationFrame: number | null = null;
  private suppressMiniPlayerClick = false;
  private openPlayerActionSheetCount = 0;
  fullscreenDismissOffset = 0;
  fullscreenDismissAnimating = false;
  private fullscreenDismissPointerId: number | null = null;
  private fullscreenDismissStartX = 0;
  private fullscreenDismissStartY = 0;
  private fullscreenDismissStartTime = 0;
  private fullscreenDismissAxis: 'pending' | 'horizontal' | 'vertical' | null = null;
  private fullscreenDismissTimer: ReturnType<typeof setTimeout> | null = null;
  private fullscreenDismissAnimationFrame: number | null = null;

  constructor(private trackGetService: TrackGetByIdEndpointService,
              private albumGetService: TrackGetAllEndpointService, private removeTrackFromPlaylistService : RemoveTrackFromPlaylistService,
              private albumByIdService: AlbumGetByIdEndpointService,
              protected musicPlayerService: MusicPlayerService, private isOnPlaylist : IsOnPlaylistService,
              private router: Router, private addTrackToLikedSongsService : AddTrackToLikedSongsService,
              private isSubscribedService : IsSubscribedService, private getPlaylistsService: GetPlaylistsByUserIdEndpointService,
              private dialog : MatDialog,private snackBar : MatSnackBar, private isLikedSongService : IsLikedSongService,
              private auth: MyUserAuthService, private playlistUpdateTracksService : PlaylistUpdateTracksService,
              private interactions: TrackInteractionService) {
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.clearSwipeTimer();
    this.clearFullscreenDismissTimer();
    this.setExpandedUi(false);
    this.musicPlayerService.clearQueue();
  }

  ngOnInit(): void {
    this.loadPlaylists();

    /*
    this.albumGetService.handleAsync({albumId: this.newTrackId}).subscribe({
      next: (response: MyPagedList<TrackGetResponse>) => {
        this.albumByIdService.handleAsync(this.newTrackId).subscribe({
          next: value => {
            this.musicPlayerService.createQueue(response.dataItems, {display:value.title+ " - " + value.type.type, value:"/artist/album/edit/"+value.id});
          }
        })
      }
    })
     */

    const request: IsSubscribedRequest = {
      userId : this.getUserIdFromToken()
    };
    this.subscriptions.add(this.isSubscribedService.handleAsync(request).subscribe({
      next: (response) => {
        if (!response.isSubscribed)
        {
          this.openPleaseSubscribeDialog();
          this.isSubbed = false;
        }
        else {
          this.isSubbed = true;
        }
      },
      error: (err) => {
        console.error('Error:', err);
      },
    }));

    this.track = this.musicPlayerService.getCurrentTrack() ?? this.musicPlayerService.getLastPlayedSong();
    if(this.track != null)
    {
      this.trackId = this.track.id;
    }

    this.playingState = this.musicPlayerService.getPlayState();
    const initialProgress = this.musicPlayerService.getPlaybackProgress();
    this.currentPlaybackTime = initialProgress.currentTime;
    this.playbackDuration = initialProgress.duration;

    this.subscriptions.add(this.musicPlayerService.currentTrack$.subscribe(data => {
      if(data == null)
      {
        return;
      }

      this.track = data;
      this.trackId = data.id;
      this.isLikedLoad();
    }));
    this.subscriptions.add(this.musicPlayerService.playStateChange.subscribe(state => {
      this.playingState = state;
    }));
    this.subscriptions.add(this.musicPlayerService.playbackProgress$.subscribe(progress => {
      this.currentPlaybackTime = progress.currentTime;
      this.playbackDuration = progress.duration;
    }));
    this.subscriptions.add(this.musicPlayerService.queuePresence$.subscribe(hasQueue => {
      if(!hasQueue && this.isExpanded)
      {
        this.setExpandedUi(false);
      }
    }));
    this.subscriptions.add(this.router.events.subscribe(event => {
      if(event instanceof NavigationStart && this.isExpanded && !this.fullscreenDismissAnimating)
      {
        this.setExpandedUi(false);
      }
    }));
  }

  protected readonly MyConfig = MyConfig;

  get coverUrl(): string {
    const path = this.track?.coverPath ?? '';
    return /^https?:\/\//i.test(path) ? path : `${MyConfig.api_address}${path}`;
  }

  get artistNames(): string {
    return this.track?.artists.map(artist => artist.name).join(', ') ?? '';
  }

  get recommendationReason(): string | null {
    const reason = this.track?.recommendationReason?.trim();
    return reason || null;
  }

  openRecommendationReason(reason: string): void {
    this.registerPlayerActionSheet(
      this.queueManager.open(
        RecommendationReasonBottomSheetComponent,
        this.playerActionSheetConfig({reason}, 'Why this track was recommended')
      )
    );
  }

  get progressPercent(): number {
    if(this.playbackDuration <= 0)
    {
      return 0;
    }

    return Math.min(100, Math.max(0, (this.currentPlaybackTime / this.playbackDuration) * 100));
  }

  get fullscreenDismissDragging(): boolean {
    return this.fullscreenDismissPointerId != null &&
      this.fullscreenDismissAxis === 'vertical' &&
      this.fullscreenDismissOffset > 0;
  }

  get fullscreenDismissTransform(): string | null {
    if(!this.isExpanded || (this.fullscreenDismissOffset <= 0 && !this.fullscreenDismissAnimating))
    {
      return null;
    }

    const viewportHeight = Math.max(1, window.innerHeight);
    const progress = Math.min(1, this.fullscreenDismissOffset / viewportHeight);
    const scale = 1 - progress * .018;
    return `translate3d(0, ${this.fullscreenDismissOffset}px, 0) scale(${scale})`;
  }

  get fullscreenDismissOpacity(): number | null {
    if(!this.isExpanded || (this.fullscreenDismissOffset <= 0 && !this.fullscreenDismissAnimating))
    {
      return null;
    }

    const progress = Math.min(1, this.fullscreenDismissOffset / Math.max(1, window.innerHeight));
    return 1 - progress * .32;
  }

  getTrackCoverUrl(track: TrackGetResponse | null): string {
    const path = track?.coverPath ?? this.track?.coverPath ?? '';
    return /^https?:\/\//i.test(path) ? path : `${MyConfig.api_address}${path}`;
  }

  getTrackArtistNames(track: TrackGetResponse | null): string {
    return track?.artists.map(artist => artist.name).join(', ') ?? this.artistNames;
  }

  getSwipeTransform(surface: 'mini' | 'cover'): string {
    if(this.swipeSurface !== surface)
    {
      return 'translate3d(-33.333333%, 0, 0)';
    }

    if(this.swipeCommitDirection === 'previous')
    {
      return 'translate3d(0, 0, 0)';
    }

    if(this.swipeCommitDirection === 'next')
    {
      return 'translate3d(-66.666667%, 0, 0)';
    }

    return `translate3d(calc(-33.333333% + ${this.swipeOffset}px), 0, 0)`;
  }

  isSwipeAnimating(surface: 'mini' | 'cover'): boolean {
    return this.swipeSurface === surface && this.swipeAnimating;
  }

  isSwiping(surface: 'mini' | 'cover'): boolean {
    return this.swipeSurface === surface && this.swipePointerId != null;
  }

  handleSwipePointerDown(event: PointerEvent, surface: 'mini' | 'cover'): void {
    if(!event.isPrimary || event.button !== 0 || this.swipeAnimating || !this.isSubbed)
    {
      return;
    }

    if(surface === 'cover' && this.isMobileViewport && !this.isExpanded)
    {
      return;
    }

    const pointerTarget = event.target instanceof Element ? event.target : null;
    if(pointerTarget?.closest('button'))
    {
      return;
    }

    const gestureTarget = event.currentTarget as HTMLElement;
    this.clearSwipeTimer();
    this.swipeSurface = surface;
    this.swipePointerId = event.pointerId;
    this.swipeStartX = event.clientX;
    this.swipeStartTime = event.timeStamp;
    this.swipeWidth = Math.max(1, gestureTarget.getBoundingClientRect().width);
    this.swipeOffset = 0;
    this.swipeCommitDirection = null;
    this.swipePreviousTrack = this.musicPlayerService.getPreviousTrack();
    this.swipeNextTrack = this.musicPlayerService.getNextTrackForGesture();
    this.suppressMiniPlayerClick = false;
    gestureTarget.setPointerCapture(event.pointerId);
  }

  handleSwipePointerMove(event: PointerEvent): void {
    if(event.pointerId !== this.swipePointerId || this.swipeSurface == null)
    {
      return;
    }

    let offset = event.clientX - this.swipeStartX;
    const targetTrack = offset > 0 ? this.swipePreviousTrack : this.swipeNextTrack;
    if(targetTrack == null)
    {
      offset *= .18;
    }

    this.swipeOffset = Math.max(-this.swipeWidth, Math.min(this.swipeWidth, offset));
    if(Math.abs(this.swipeOffset) > 8)
    {
      this.suppressMiniPlayerClick = true;
    }

    event.preventDefault();
  }

  handleSwipePointerEnd(event: PointerEvent): void {
    if(event.pointerId !== this.swipePointerId || this.swipeSurface == null)
    {
      return;
    }

    const completedSurface = this.swipeSurface;
    const target = event.currentTarget as HTMLElement;
    if(target.hasPointerCapture(event.pointerId))
    {
      target.releasePointerCapture(event.pointerId);
    }

    const distance = this.swipeOffset;
    const elapsed = Math.max(1, event.timeStamp - this.swipeStartTime);
    const velocity = distance / elapsed;
    const direction: 'previous' | 'next' = distance > 0 ? 'previous' : 'next';
    const targetTrack = direction === 'previous' ? this.swipePreviousTrack : this.swipeNextTrack;
    const passedDistanceThreshold = Math.abs(distance) >= Math.max(52, this.swipeWidth * .18);
    const passedVelocityThreshold = Math.abs(distance) >= 18 && Math.abs(velocity) >= .45;

    this.swipePointerId = null;

    if(Math.abs(distance) < 8)
    {
      this.resetSwipeState();
      if(completedSurface === 'mini')
      {
        this.openExpandedPlayer();
      }
      return;
    }

    this.suppressMiniPlayerClick = true;
    if(targetTrack != null && (passedDistanceThreshold || passedVelocityThreshold))
    {
      this.animateSwipe(direction, targetTrack);
      return;
    }

    this.animateSwipe(null, null);
  }

  handleSwipePointerCancel(event: PointerEvent): void {
    if(event.pointerId !== this.swipePointerId)
    {
      return;
    }

    this.swipePointerId = null;
    this.suppressMiniPlayerClick = Math.abs(this.swipeOffset) >= 8;
    this.animateSwipe(null, null);
  }

  handleFullscreenDismissPointerDown(event: PointerEvent): void {
    if(
      !this.isExpanded ||
      !this.isMobileViewport ||
      !event.isPrimary ||
      event.button !== 0 ||
      this.fullscreenDismissAnimating ||
      this.isPlayerActionSheetOpen
    )
    {
      return;
    }

    const target = event.target instanceof Element ? event.target : null;
    if(target?.closest(
      'button, a, input, mat-slider, .clickable-artists, .secondary-actions, .control-bar, .volume-slider'
    ))
    {
      return;
    }

    this.clearFullscreenDismissTimer();
    this.fullscreenDismissPointerId = event.pointerId;
    this.fullscreenDismissStartX = event.clientX;
    this.fullscreenDismissStartY = event.clientY;
    this.fullscreenDismissStartTime = event.timeStamp;
    this.fullscreenDismissAxis = 'pending';
    this.fullscreenDismissOffset = 0;
  }

  handleFullscreenDismissPointerMove(event: PointerEvent): void {
    if(event.pointerId !== this.fullscreenDismissPointerId || this.fullscreenDismissAxis == null)
    {
      return;
    }

    const deltaX = event.clientX - this.fullscreenDismissStartX;
    const deltaY = event.clientY - this.fullscreenDismissStartY;
    if(this.fullscreenDismissAxis === 'pending')
    {
      if(Math.max(Math.abs(deltaX), Math.abs(deltaY)) < 8)
      {
        return;
      }

      if(Math.abs(deltaY) > Math.abs(deltaX) * 1.15)
      {
        this.fullscreenDismissAxis = 'vertical';
        this.suppressMiniPlayerClick = false;
        if(this.swipePointerId === event.pointerId)
        {
          this.resetSwipeState();
        }
      }
      else if(Math.abs(deltaX) > Math.abs(deltaY) * 1.15)
      {
        this.fullscreenDismissAxis = 'horizontal';
      }
      else
      {
        return;
      }
    }

    if(this.fullscreenDismissAxis !== 'vertical')
    {
      return;
    }

    this.fullscreenDismissOffset = deltaY > 0
      ? Math.min(window.innerHeight, deltaY)
      : Math.max(-10, deltaY * .08);
    event.preventDefault();
  }

  handleFullscreenDismissPointerEnd(event: PointerEvent): void {
    if(event.pointerId !== this.fullscreenDismissPointerId)
    {
      return;
    }

    const elapsed = Math.max(1, event.timeStamp - this.fullscreenDismissStartTime);
    const velocity = this.fullscreenDismissOffset / elapsed;
    const threshold = Math.min(150, window.innerHeight * .16);
    const shouldClose = this.fullscreenDismissAxis === 'vertical' &&
      (
        this.fullscreenDismissOffset >= threshold ||
        (this.fullscreenDismissOffset >= 36 && velocity >= .5)
      );

    this.fullscreenDismissPointerId = null;
    this.fullscreenDismissAxis = null;
    this.animateFullscreenDismiss(shouldClose);
  }

  handleFullscreenDismissPointerCancel(event: PointerEvent): void {
    if(event.pointerId !== this.fullscreenDismissPointerId)
    {
      return;
    }

    this.fullscreenDismissPointerId = null;
    this.fullscreenDismissAxis = null;
    this.animateFullscreenDismiss(false);
  }

  handleMiniPlayerClick(event: MouseEvent): void {
    if(this.suppressMiniPlayerClick)
    {
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    this.openExpandedPlayer();
  }

  openExpandedPlayer(): void {
    if(this.track == null || this.musicPlayerService.queue.length === 0 || this.isExpanded)
    {
      return;
    }

    history.pushState({...history.state, listenerPlayerExpanded: true}, '');
    this.setExpandedUi(true);
  }

  closeExpandedPlayer(): void {
    if(!this.isExpanded || this.fullscreenDismissAnimating)
    {
      return;
    }

    if(this.isMobileViewport)
    {
      this.animateFullscreenDismiss(true);
      return;
    }

    this.finalizeExpandedClose();
  }

  private finalizeExpandedClose(): void {
    const shouldRestoreHistory = history.state?.listenerPlayerExpanded === true;
    this.setExpandedUi(false);
    if(shouldRestoreHistory)
    {
      history.back();
    }
  }

  togglePlayback(event: Event): void {
    event.stopPropagation();
    if(!this.isSubbed)
    {
      this.openPleaseSubscribeDialog();
      return;
    }

    this.musicPlayerService.togglePlayState();
  }

  skipNext(event: Event): void {
    event.stopPropagation();
    if(!this.isSubbed)
    {
      return;
    }

    if(this.musicController != null)
    {
      this.musicController.skipNext();
      return;
    }

    this.musicPlayerService.playNext();
  }

  handlePlaybackSkip(direction: 'previous' | 'next'): void {
    if(this.swipeAnimating || !this.isSubbed)
    {
      return;
    }

    const controller = this.musicController;
    if(direction === 'previous' && controller != null && !controller.willNavigateToPreviousTrack())
    {
      controller.skipPrevious();
      return;
    }

    if(direction === 'next' && controller?.isLooping)
    {
      controller.skipNext();
      return;
    }

    const previousTrack = this.musicPlayerService.getPreviousTrack();
    const nextTrack = this.musicPlayerService.getNextTrackForGesture();
    const targetTrack = direction === 'previous' ? previousTrack : nextTrack;

    if(targetTrack == null)
    {
      if(direction === 'previous')
      {
        controller?.skipPrevious();
      }
      else if(controller != null)
      {
        controller.skipNext();
      }
      else
      {
        this.musicPlayerService.playNext();
      }
      return;
    }

    this.clearSwipeTimer();
    this.swipeSurface = 'cover';
    this.swipePointerId = null;
    this.swipeOffset = 0;
    this.swipeCommitDirection = null;
    this.swipePreviousTrack = previousTrack;
    this.swipeNextTrack = nextTrack;
    this.swipeAnimating = true;

    this.swipeAnimationFrame = window.requestAnimationFrame(() => {
      this.swipeAnimationFrame = null;
      if(this.swipeSurface !== 'cover')
      {
        return;
      }

      this.animateSwipe(direction, targetTrack, () => {
        if(direction === 'previous')
        {
          this.musicPlayerService.playPrev();
          return;
        }

        if(controller != null)
        {
          controller.skipToGestureTrack(targetTrack);
          return;
        }

        this.musicPlayerService.playQueuedTrack(targetTrack);
      });
    });
  }

  private animateSwipe(
    direction: 'previous' | 'next' | null,
    targetTrack: TrackGetResponse | null,
    onCommit?: () => void
  ): void {
    this.swipeAnimating = true;
    this.swipeCommitDirection = direction;
    if(direction == null)
    {
      this.swipeOffset = 0;
    }

    this.clearSwipeTimer();
    this.swipeTimer = setTimeout(() => {
      if(direction != null && onCommit != null)
      {
        onCommit();
      }
      else if(direction === 'previous')
      {
        this.musicPlayerService.playPrev();
      }
      else if(direction === 'next' && targetTrack != null)
      {
        if(this.musicController != null)
        {
          this.musicController.skipToGestureTrack(targetTrack);
        }
        else
        {
          this.musicPlayerService.playQueuedTrack(targetTrack);
        }
      }

      this.resetSwipeState();
      this.swipeTimer = setTimeout(() => {
        this.suppressMiniPlayerClick = false;
        this.swipeTimer = null;
      }, 80);
    }, 240);
  }

  private resetSwipeState(): void {
    this.swipePointerId = null;
    this.swipeOffset = 0;
    this.swipeAnimating = false;
    this.swipeCommitDirection = null;
    this.swipeSurface = null;
    this.swipePreviousTrack = null;
    this.swipeNextTrack = null;
  }

  private clearSwipeTimer(): void {
    if(this.swipeAnimationFrame != null)
    {
      window.cancelAnimationFrame(this.swipeAnimationFrame);
      this.swipeAnimationFrame = null;
    }

    if(this.swipeTimer != null)
    {
      clearTimeout(this.swipeTimer);
      this.swipeTimer = null;
    }
  }

  handleMiniPlayerKeydown(event: KeyboardEvent): void {
    if(event.key !== 'Enter' && event.key !== ' ')
    {
      return;
    }

    event.preventDefault();
    this.openExpandedPlayer();
  }

  @HostListener('window:popstate')
  handlePopState(): void {
    if(this.isExpanded)
    {
      this.setExpandedUi(false);
    }
  }

  @HostListener('window:keydown.escape')
  handleEscape(): void {
    this.closeExpandedPlayer();
  }

  @HostListener('window:resize')
  handleResize(): void {
    this.isMobileViewport = window.matchMedia('(max-width: 960px)').matches;
    if(!this.isMobileViewport && this.isExpanded)
    {
      this.setExpandedUi(false);
    }
  }

  private setExpandedUi(expanded: boolean): void {
    this.isExpanded = expanded;
    if(!expanded)
    {
      this.suppressMiniPlayerClick = false;
      this.resetSwipeState();
      this.resetFullscreenDismissState();
    }
    document.body.classList.toggle('listener-player-expanded', expanded);

    const routedContent = document.querySelector<HTMLElement>('.routed-content');
    const mobileNavigation = document.querySelector<HTMLElement>('.mobile-bottom-nav');
    [routedContent, mobileNavigation].forEach(element => {
      if(element == null)
      {
        return;
      }

      if(expanded)
      {
        element.setAttribute('inert', '');
        element.setAttribute('aria-hidden', 'true');
      }
      else
      {
        element.removeAttribute('inert');
        element.removeAttribute('aria-hidden');
      }
    });
  }

  private animateFullscreenDismiss(shouldClose: boolean): void {
    if(this.fullscreenDismissOffset <= 0 && !shouldClose)
    {
      this.resetFullscreenDismissState();
      return;
    }

    this.clearFullscreenDismissTimer();
    this.fullscreenDismissAnimating = true;
    const targetOffset = shouldClose
      ? Math.max(window.innerHeight * 1.04, this.fullscreenDismissOffset)
      : 0;

    this.fullscreenDismissAnimationFrame = window.requestAnimationFrame(() => {
      this.fullscreenDismissAnimationFrame = null;
      this.fullscreenDismissOffset = targetOffset;

      this.fullscreenDismissTimer = setTimeout(() => {
        this.fullscreenDismissTimer = null;
        if(shouldClose)
        {
          this.finalizeExpandedClose();
          return;
        }

        this.fullscreenDismissAnimating = false;
      }, 220);
    });
  }

  private resetFullscreenDismissState(): void {
    this.clearFullscreenDismissTimer();
    this.fullscreenDismissPointerId = null;
    this.fullscreenDismissAxis = null;
    this.fullscreenDismissOffset = 0;
    this.fullscreenDismissAnimating = false;
  }

  private clearFullscreenDismissTimer(): void {
    if(this.fullscreenDismissAnimationFrame != null)
    {
      window.cancelAnimationFrame(this.fullscreenDismissAnimationFrame);
      this.fullscreenDismissAnimationFrame = null;
    }

    if(this.fullscreenDismissTimer != null)
    {
      clearTimeout(this.fullscreenDismissTimer);
      this.fullscreenDismissTimer = null;
    }
  }

  setNewSong() {
    this.trackGetService.handleAsync(this.newTrackId).subscribe({
      next: data => {
        this.musicPlayerService.addToQueue(data);
        this.trackId = data.id;
        console.log(this.musicPlayerService.queue);
      }
    })
  }

  openQueueManager() {
    let queue = this.musicPlayerService.getQueue();
    this.registerPlayerActionSheet(
      this.queueManager.open(
        QueueViewBottomSheetComponent,
        this.playerActionSheetConfig({queue}, 'Manage queue')
      )
    );
  }

  openShareSheet() {
    this.registerPlayerActionSheet(
      this.queueManager.open(
        ShareBottomSheetComponent,
        this.playerActionSheetConfig(
          {url: MyConfig.ui_address + "/listener/track/" + this.track?.id},
          'Share track'
        )
      )
    );
  }
  isLikedLoad() {
    if(this.trackId <= 0)
    {
      return;
    }

    const requestLike = { trackId: this.trackId, userId: this.getUserIdFromToken() };
    this.isLikedSongService.handleAsync(requestLike).subscribe({
      next: response => {
        console.log("This song is", response);
        this.likedSongs.set(this.trackId, response.isLikedSong);
      },
    });
  }
  redirectToSource() {
    this.navigateFromPlayer([this.musicPlayerService.queueSource.value]);
  }

  goToRelease() {
    if(this.track?.albumId == null)
    {
      return;
    }

    this.navigateFromPlayer(["/listener/release", this.track.albumId]);
  }

  goHome() {
    this.router.navigate(["/listener/home"]);
  }

  goToSearch() {
    this.router.navigate(["/listener/search"]);
  }

  goToSubscriptions(): void {
    this.navigateFromPlayer(['/listener/subscriptions']);
  }

  prepareForPlayerNavigation(): void {
    if(!this.isExpanded || this.fullscreenDismissAnimating)
    {
      return;
    }

    if(this.isMobileViewport)
    {
      this.animateFullscreenDismiss(true);
      return;
    }

    this.finalizeExpandedClose();
  }

  messageBottomSheet() {
    /*
    this.musicPlayerService.setAutoPlayStatus(!this.musicPlayerService.getAutoPlayStatus());
    console.log(this.musicPlayerService.getAutoPlayStatus());
     */
    this.registerPlayerActionSheet(
      this.queueManager.open(
        SendSongMessageComponent,
        this.playerActionSheetConfig({track: this.track}, 'Send track')
      )
    );
  }

  openStemMixerSheet(): void {
    if(this.musicController == null)
    {
      return;
    }

    this.registerPlayerActionSheet(
      this.queueManager.open(
        StemMixerBottomSheetComponent,
        this.playerActionSheetConfig(
          {controller: this.musicController},
          'Stem mixer'
        )
      )
    );
  }

  private openPleaseSubscribeDialog(): void {
    const dialogRef = this.dialog.open(PleaseSubscribeComponent, {
      width: '400px',
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result: string | undefined ) => {
      if (result === 'navigate') {
        this.router.navigate(['listener/subscriptions']);
      }
    });
  }

  playlistDropdown(id: number | undefined) {
    if(id == null)
    {
      return;
    }

    this.selectedTrackId = id;
    const checks = this.playlists.map(playlist =>
      this.isOnPlaylist.handleAsync({playlistId: playlist.id, trackId: id}).pipe(
        catchError(() => of({isAlreadyOnPlaylist: false}))
      )
    );

    const membershipRequest = checks.length > 0 ? forkJoin(checks) : of([]);
    membershipRequest.subscribe(results => {
      const membership: Record<number, boolean> = {};
      this.playlists.forEach((playlist, index) => {
        const isMember = results[index]?.isAlreadyOnPlaylist ?? false;
        membership[playlist.id] = isMember;
        if(!this.playlistTrackMap.has(playlist.id))
        {
          this.playlistTrackMap.set(playlist.id, new Map());
        }
        this.playlistTrackMap.get(playlist.id)!.set(id, isMember);
      });

      const sheetRef = this.registerPlayerActionSheet(
        this.queueManager.open<
          AddToPlaylistBottomSheetComponent,
          {playlists: PlaylistResponse[]; membership: Record<number, boolean>},
          AddToPlaylistBottomSheetResult
        >(
          AddToPlaylistBottomSheetComponent,
          this.playerActionSheetConfig(
            {playlists: this.playlists, membership},
            'Add track to playlist'
          )
        )
      );

      sheetRef.afterDismissed().subscribe(result => {
        if(result != null)
        {
          this.addToPlaylist(result.playlistId);
        }
      });
    });
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
  addToPlaylist(playlistId: number) {
    if (this.selectedTrackId) {
      const isInPlaylist = this.isTrackInPlaylist(this.selectedTrackId, playlistId);

      if (isInPlaylist) {
        this.removeTrackFromPlaylistService.handleAsync(playlistId, this.selectedTrackId).subscribe({
          next: () => {
            this.snackBar.open('Track removed from playlist!', 'Dismiss', { duration: 3500 });
            this.playlistTrackMap.get(playlistId)?.set(this.selectedTrackId!, false);
          },
          error: (error) => {
            console.error('Error removing track from playlist:', error);
          },
        });
      } else {
        const request: PlaylistUpdateTracksRequest = {
          playlistId: playlistId,
          userId : this.getUserIdFromToken(),
          trackIds: [this.selectedTrackId],
        };

        this.playlistUpdateTracksService.handleAsync(request).subscribe({
          next: () => {
            this.snackBar.open('Track added to playlist!', 'Dismiss', { duration: 3500 });
            if(!this.playlistTrackMap.has(playlistId))
            {
              this.playlistTrackMap.set(playlistId, new Map());
            }
            this.playlistTrackMap.get(playlistId)!.set(this.selectedTrackId, true);
          },
          error: (error) => {
            console.error('Error adding track to playlist:', error);
          },
        });
      }
    }
  }

  getLikeIcon(id: number | undefined) {
    return this.likedSongs.get(id!) ? 'favorite' : 'favorite_border';
  }

  addToLikedSongs(id: number | undefined) {
    const isLiked = this.likedSongs.get(id!) || false;
    const request = { trackId: id!, userId: this.getUserIdFromToken() };

    if (isLiked) {
      this.addTrackToLikedSongsService.handleAsync(request).subscribe({
        next: () => {
          this.likedSongs.set(id!, false);
          this.interactions.record(id!, 'Unliked', {contextType: this.getInteractionContext()});
          this.snackBar.open("Song removed from liked songs", "Dismiss", { duration: 3500 });
        },
        error: error => {
          console.error('Error removing track:', error);
        },
      });
    } else {
      this.addTrackToLikedSongsService.handleAsync(request).subscribe({
        next: () => {
          this.likedSongs.set(id!, true);
          this.interactions.record(id!, 'Liked', {contextType: this.getInteractionContext()});
          this.snackBar.open("Song added to liked songs", "Dismiss", { duration: 3500 });
        },
        error: error => {
          console.error('Error adding track:', error);
        },
      });
    }
  }

  goToTrackRadio(): void {
    if(this.track == null)
    {
      return;
    }

    this.navigateFromPlayer(['/listener/radio', this.track.id]);
  }

  private navigateFromPlayer(commands: Array<string | number>): void {
    const replaceUrl = this.isExpanded && history.state?.listenerPlayerExpanded === true;
    this.prepareForPlayerNavigation();
    void this.router.navigate(commands, {replaceUrl});
  }

  private getInteractionContext() {
    switch(this.musicPlayerService.getQueueType())
    {
      case 'autoplay': return 'Autoplay' as const;
      case 'radio': return 'Radio' as const;
      case 'playlist':
      case 'personalized-playlist': return 'Playlist' as const;
      case 'song': return 'Manual' as const;
      default: return 'Playback' as const;
    }
  }
  initializePlaylistCheckboxes(trackId: number): void {
    if (!this.playlists.length) return;

    this.playlists.forEach(playlist => {
      const request = { playlistId: playlist.id, trackId: trackId };
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
  isTrackInPlaylist(trackId : number, playlistId: number) {
    return this.playlistTrackMap.get(playlistId)?.get(trackId) ?? false;
  }

  private playerActionSheetConfig<T>(data: T, ariaLabel: string): MatBottomSheetConfig<T> {
    return {
      data,
      ariaLabel,
      hasBackdrop: true,
      restoreFocus: true,
      panelClass: ['liquid-glass-sheet-pane', 'player-action-sheet-pane'],
      backdropClass: 'liquid-glass-sheet-backdrop'
    };
  }

  private registerPlayerActionSheet<T, R>(
    sheetRef: MatBottomSheetRef<T, R>
  ): MatBottomSheetRef<T, R> {
    this.openPlayerActionSheetCount++;
    this.isPlayerActionSheetOpen = true;

    sheetRef.afterDismissed().subscribe(() => {
      this.openPlayerActionSheetCount = Math.max(0, this.openPlayerActionSheetCount - 1);
      this.isPlayerActionSheetOpen = this.openPlayerActionSheetCount > 0;
    });

    return sheetRef;
  }
}
