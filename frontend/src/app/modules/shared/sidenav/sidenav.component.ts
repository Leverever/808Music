import {ChangeDetectorRef, Component, HostListener, OnDestroy, OnInit} from '@angular/core';
import {NavigationEnd, Router} from '@angular/router';
import {ArtistHandlerService} from '../../../services/artist-handler.service';
import {MyConfig} from "../../../my-config";
import {UserProfileService} from '../../../endpoints/auth-endpoints/user-profile-endpoint.service';
import {PfpCropperDialogComponent} from '../pfp-cropper-dialog/pfp-cropper-dialog.component';
import {MatDialog} from '@angular/material/dialog';
import {NotificationsService, RichNotification} from '../../../services/notifications.service';
import {ChatService} from '../../../services/chat.service';
import {Subscription} from 'rxjs';
import {
  GetUserUnreadsEndpointService,
  UnreadsResponse
} from '../../../endpoints/user-endpoints/get-user-unreads-endpoint.service';
import {MonthlyStatsDialogComponent} from '../../listener/monthly-stats-dialog/monthly-stats-dialog.component';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';

@Component({
  selector: 'app-sidenav',
  templateUrl: './sidenav.component.html',
  styleUrls: ['./sidenav.component.css']
})
export class SidenavComponent implements OnInit, OnDestroy {
  isMenuVisible: boolean = false;
  mobileMenuVisible = false;
  activeMobileNavIndex = 0;
  mobileNavIndicatorOffset = '0%';
  previousMobileNavIndicatorOffset = '0%';
  navIndicatorMoving = false;
  mobileMenuDragOffset = 0;
  mobileMenuDragging = false;
  mobileMenuHasDragged = false;
  mobileMenuDismissing = false;
  pathToPfp = MyConfig.media_address;
  isAdmin = false;

  unreads : UnreadsResponse | null = null;
  notiReceive = (noti : RichNotification) => {
    if(this.unreads == null)
    {
      this.getUnreadsCount();
      return;
    }

    if(noti.type === "Message")
    {
      if(this.router.url.includes("/chat"))
      {
        return;
      }

      this.unreads.unreadMessaggesCount = Math.min(this.unreads.unreadMessaggesCount + 1, 99);
      return;
    }

    this.unreads.unreadNotificationsCount = Math.min(this.unreads.unreadNotificationsCount + 1, 99);
  }
  chat$ : Subscription | null = null;
  private subscriptions = new Subscription();
  private mobileRouteIndex = 0;
  private navIndicatorAnimationFrame: number | null = null;
  private navIndicatorAnimationTimer: ReturnType<typeof setTimeout> | null = null;
  private mobileMenuDragPointerId: number | null = null;
  private mobileMenuDragStartY = 0;
  private mobileMenuDragLastY = 0;
  private mobileMenuDragLastTime = 0;
  private mobileMenuDragVelocity = 0;
  private mobileMenuDismissDistance = 120;
  private mobileMenuDragHandle: HTMLElement | null = null;
  private mobileMenuDismissTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private router: Router,
              private cdRef: ChangeDetectorRef,
              private dialog: MatDialog,
              private userProfileService : UserProfileService,
              private artistHandlerService: ArtistHandlerService,
              private notificationService: NotificationsService,
              private chatService: ChatService,
              private getUserUnreadsService : GetUserUnreadsEndpointService,
              private authService: MyUserAuthService){}

  toggleMenu(): void {
    this.isMenuVisible = !this.isMenuVisible;
  }

  toggleMobileMenu(): void {
    this.resetMobileMenuDrag();
    this.mobileMenuVisible = !this.mobileMenuVisible;
    this.isMenuVisible = false;
    this.moveMobileNavIndicator(this.mobileMenuVisible ? 4 : this.mobileRouteIndex);
  }

  closeMobileMenu(): void {
    this.resetMobileMenuDrag();
    this.mobileMenuVisible = false;
    this.moveMobileNavIndicator(this.mobileRouteIndex);
  }

  selectMobileNav(index: number): void {
    this.resetMobileMenuDrag();
    this.mobileMenuVisible = false;
    this.moveMobileNavIndicator(index);
  }

  startMobileMenuDrag(event: PointerEvent): void {
    if(
      !this.mobileMenuVisible ||
      this.mobileMenuDismissing ||
      !event.isPrimary ||
      (event.pointerType === 'mouse' && event.button !== 0)
    )
    {
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    const handle = event.currentTarget as HTMLElement;
    const sheet = handle.closest('.mobile-more-sheet') as HTMLElement | null;
    this.mobileMenuDismissDistance = sheet
      ? Math.min(160, Math.max(88, sheet.getBoundingClientRect().height * .24))
      : 120;
    this.mobileMenuDragPointerId = event.pointerId;
    this.mobileMenuDragStartY = event.clientY;
    this.mobileMenuDragLastY = event.clientY;
    this.mobileMenuDragLastTime = event.timeStamp;
    this.mobileMenuDragVelocity = 0;
    this.mobileMenuDragOffset = 0;
    this.mobileMenuDragging = true;
    this.mobileMenuHasDragged = true;
    this.mobileMenuDragHandle = handle;
    handle.setPointerCapture?.(event.pointerId);
  }

  @HostListener('document:pointermove', ['$event'])
  onMobileMenuDragMove(event: PointerEvent): void {
    if(
      !this.mobileMenuDragging ||
      this.mobileMenuDragPointerId !== event.pointerId
    )
    {
      return;
    }

    event.preventDefault();
    const now = event.timeStamp;
    const elapsed = Math.max(1, now - this.mobileMenuDragLastTime);
    const instantVelocity = (event.clientY - this.mobileMenuDragLastY) / elapsed;
    this.mobileMenuDragVelocity = (this.mobileMenuDragVelocity * .35) + (instantVelocity * .65);
    this.mobileMenuDragOffset = Math.max(0, event.clientY - this.mobileMenuDragStartY);
    this.mobileMenuDragLastY = event.clientY;
    this.mobileMenuDragLastTime = now;
  }

  @HostListener('document:pointerup', ['$event'])
  onMobileMenuDragEnd(event: PointerEvent): void {
    this.finishMobileMenuDrag(event, false);
  }

  @HostListener('document:pointercancel', ['$event'])
  onMobileMenuDragCancel(event: PointerEvent): void {
    this.finishMobileMenuDrag(event, true);
  }

  get mobileMenuBackdropOpacity(): number {
    if(this.mobileMenuDismissing)
    {
      return 0;
    }

    const fadeDistance = Math.max(this.mobileMenuDismissDistance * 1.7, 180);
    return Math.max(.2, 1 - (this.mobileMenuDragOffset / fadeDistance));
  }

  get totalUnreadCount(): number {
    return (this.unreads?.unreadNotificationsCount ?? 0) + (this.unreads?.unreadMessaggesCount ?? 0);
  }

  formatUnreadCount(count: number): string {
    return count > 99 ? '99+' : count.toString();
  }

  getUnreadsCount() {
    this.getUserUnreadsService.handleAsync().subscribe(unreads => {
      this.unreads = unreads;
    })
  }

  ngOnInit(): void {
    this.isAdmin = this.authService.isAdmin();
    const userId = this.getUserIdFromToken();
    console.log('User ID:', userId);
    this.mobileRouteIndex = this.getMobileNavIndex(this.router.url);
    this.setInitialMobileNavIndicator(this.mobileRouteIndex);

    this.subscriptions.add(this.router.events.subscribe(event => {
      if(event instanceof NavigationEnd) {
        this.isMenuVisible = false;
        this.mobileMenuVisible = false;
        this.mobileRouteIndex = this.getMobileNavIndex(event.urlAfterRedirects);
        this.moveMobileNavIndicator(this.mobileRouteIndex);
        this.getUnreadsCount();
      }
    }));

    if (userId) {
      this.userProfileService.getProfilePicture(userId).subscribe(
        (response) => {
          if (response && response.profilePicturePath) {
            this.pathToPfp = MyConfig.media_address + response.profilePicturePath;
            this.cdRef.detectChanges();
          }
        },
        (error) => {
          console.error('Error fetching profile picture:', error);
        }
      );
    } else {
      console.error('No user ID found.');
    }

    this.notificationService.addNotificationListener(this.notiReceive);
    this.chat$ = this.chatService.msgNotify$.subscribe(this.notiReceive);
    this.getUnreadsCount();
  }

  ngOnDestroy(): void {
    this.notificationService.removeNotificationListener(this.notiReceive);
    this.chat$?.unsubscribe();
    this.subscriptions.unsubscribe();
    if(this.navIndicatorAnimationFrame != null)
    {
      cancelAnimationFrame(this.navIndicatorAnimationFrame);
    }
    if(this.navIndicatorAnimationTimer != null)
    {
      clearTimeout(this.navIndicatorAnimationTimer);
    }
    this.resetMobileMenuDrag();
  }
  openImageCropperDialog(): void {
    const dialogRef = this.dialog.open(PfpCropperDialogComponent, {
      width: '500px',
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        console.log('Profile picture uploaded successfully:', result);
        this.pathToPfp = MyConfig.media_address + result.profilePicturePath;
        this.cdRef.detectChanges();
      } else {
        console.error('Profile picture upload failed');
      }
    });
    this.cdRef.detectChanges();

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
  openDialog(): void {
    const dialogRef = this.dialog.open(MonthlyStatsDialogComponent, {
      width: 'calc(100vw - 16px)',
      maxWidth: '548px',
      maxHeight: 'calc(100dvh - 16px)',
      autoFocus: false,
    });

    dialogRef.afterClosed().subscribe(result => {
      console.log('The dialog was closed');
    });
  }
  navigateTo(destination: string): void {
    if (destination === 'products') {
      const selectedArtistName = this.artistHandlerService.getSelectedArtist();
      console.log('Selected Artist Name:', selectedArtistName);

      if (selectedArtistName) {
        this.router.navigate([`/artist/${selectedArtistName.name}/products`]);
      } else {
        console.error('No artist selected');
        alert('No artist selected.');
      }
    } else {
      this.router.navigate([destination]);
    }
    this.isMenuVisible = false;
    this.resetMobileMenuDrag();
    this.mobileMenuVisible = false;
  }


  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event): void {
    const clickedElement = event.target as HTMLElement;
    const isInsideMenu =
      clickedElement.closest('.profile-menu') || clickedElement.classList.contains('profile-picture');

    const isInsideMobileNavigation =
      clickedElement.closest('.mobile-more-sheet') || clickedElement.closest('.mobile-bottom-nav');

    if (!isInsideMenu) {
      this.isMenuVisible = false;
    }
    if (!isInsideMobileNavigation && this.mobileMenuVisible) {
      this.closeMobileMenu();
    }
  }

    protected readonly MyConfig = MyConfig;

  openUserProfile() {
    const userId = this.getUserIdFromToken();
    this.isMenuVisible = false;
    this.resetMobileMenuDrag();
    this.mobileMenuVisible = false;
    this.router.navigate([`/listener/user/`,userId]);
  }

  goHome() {
    this.router.navigate([`/listener/home/`]);

  }

  private getMobileNavIndex(url: string): number {
    const path = url.split('?')[0].split('#')[0];
    if(path.startsWith('/listener/home'))
    {
      return 0;
    }
    if(path.startsWith('/listener/search'))
    {
      return 1;
    }
    if(path.startsWith('/listener/playlist'))
    {
      return 2;
    }
    if(
      path.startsWith('/listener/store') ||
      path.startsWith('/listener/product') ||
      path.startsWith('/listener/checkout')
    )
    {
      return 3;
    }
    return 4;
  }

  private setInitialMobileNavIndicator(index: number): void {
    this.activeMobileNavIndex = index;
    this.mobileNavIndicatorOffset = `${index * 100}%`;
    this.previousMobileNavIndicatorOffset = this.mobileNavIndicatorOffset;
  }

  private moveMobileNavIndicator(index: number): void {
    const nextIndex = Math.max(0, Math.min(4, index));
    if(nextIndex === this.activeMobileNavIndex)
    {
      return;
    }

    this.previousMobileNavIndicatorOffset = this.mobileNavIndicatorOffset;
    this.activeMobileNavIndex = nextIndex;
    this.mobileNavIndicatorOffset = `${nextIndex * 100}%`;
    this.navIndicatorMoving = false;

    if(this.navIndicatorAnimationFrame != null)
    {
      cancelAnimationFrame(this.navIndicatorAnimationFrame);
    }
    if(this.navIndicatorAnimationTimer != null)
    {
      clearTimeout(this.navIndicatorAnimationTimer);
    }

    this.cdRef.detectChanges();
    this.navIndicatorAnimationFrame = requestAnimationFrame(() => {
      this.navIndicatorAnimationFrame = null;
      this.navIndicatorMoving = true;
      this.cdRef.detectChanges();
      this.navIndicatorAnimationTimer = setTimeout(() => {
        this.navIndicatorMoving = false;
        this.navIndicatorAnimationTimer = null;
        this.cdRef.detectChanges();
      }, 460);
    });
  }

  private finishMobileMenuDrag(event: PointerEvent, cancelled: boolean): void {
    if(
      !this.mobileMenuDragging ||
      this.mobileMenuDragPointerId !== event.pointerId
    )
    {
      return;
    }

    this.releaseMobileMenuPointerCapture();
    this.mobileMenuDragging = false;
    this.mobileMenuDragPointerId = null;

    const passedDistance = this.mobileMenuDragOffset >= this.mobileMenuDismissDistance;
    const flickedDown = this.mobileMenuDragOffset >= 24 && this.mobileMenuDragVelocity >= .65;
    if(!cancelled && (passedDistance || flickedDown))
    {
      this.mobileMenuDismissing = true;
      const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      this.mobileMenuDismissTimer = setTimeout(() => {
        this.mobileMenuDismissTimer = null;
        this.mobileMenuVisible = false;
        this.mobileMenuDismissing = false;
        this.mobileMenuDragOffset = 0;
        this.moveMobileNavIndicator(this.mobileRouteIndex);
        this.cdRef.detectChanges();
      }, reducedMotion ? 0 : 220);
      return;
    }

    this.mobileMenuDragOffset = 0;
    this.mobileMenuDragVelocity = 0;
  }

  private releaseMobileMenuPointerCapture(): void {
    if(
      this.mobileMenuDragHandle != null &&
      this.mobileMenuDragPointerId != null &&
      this.mobileMenuDragHandle.hasPointerCapture?.(this.mobileMenuDragPointerId)
    )
    {
      this.mobileMenuDragHandle.releasePointerCapture(this.mobileMenuDragPointerId);
    }
    this.mobileMenuDragHandle = null;
  }

  private resetMobileMenuDrag(): void {
    this.releaseMobileMenuPointerCapture();
    this.mobileMenuDragPointerId = null;
    this.mobileMenuDragOffset = 0;
    this.mobileMenuDragVelocity = 0;
    this.mobileMenuDragging = false;
    this.mobileMenuHasDragged = false;
    this.mobileMenuDismissing = false;
    if(this.mobileMenuDismissTimer != null)
    {
      clearTimeout(this.mobileMenuDismissTimer);
      this.mobileMenuDismissTimer = null;
    }
  }
}
