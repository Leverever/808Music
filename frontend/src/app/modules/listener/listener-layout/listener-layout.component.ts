import {Component, OnDestroy, OnInit} from '@angular/core';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';
import {NotificationsService, RichNotification} from '../../../services/notifications.service';
import {MatSnackBar} from '@angular/material/snack-bar';
import {MusicPlayerService} from '../../../services/music-player.service';
import {
  IsSubscribedRequest,
  IsSubscribedService
} from '../../../endpoints/auth-endpoints/is-subscribed-endpoint.service';
import {
  PlaylistUpdateTracksRequest
} from '../../../endpoints/playlist-endpoints/add-track-to-playlist-endpoint.service';
import {PleaseSubscribeComponent} from '../../shared/bottom-sheets/please-subscribe/please-subscribe.component';
import {MatDialog} from '@angular/material/dialog';
import {Router} from '@angular/router';
import {ChatService} from '../../../services/chat.service';
import {Subscription} from 'rxjs';
import {MessageGetResponse} from '../../../endpoints/chat-endpoints/chat-get-messages-endpoint.service';

@Component({
  selector: 'app-listener-layout',
  templateUrl: './listener-layout.component.html',
  styleUrl: './listener-layout.component.css'
})
export class ListenerLayoutComponent implements OnInit, OnDestroy {
  showPleaseSubscribe = false;
  chat$ : Subscription | null = null;

  notiCallback = (data:RichNotification) => {
    if(data.type === "Message" && this.router.url.includes("/chat"))
    {
      return;
    }

    let snackRef = this.snackBar.open(data.message, "View", {duration: 2000});
    let audio = new Audio('assets/notification.mp3');
    audio.play().catch(err => {console.log(err)});

    snackRef.afterDismissed().subscribe(data => {
      if(data.dismissedByAction)
      {
        this.router.navigate(['/listener/notifications']);
      }
    });
  }

  constructor(private auth: MyUserAuthService,
              private router: Router,
              private notificationsService: NotificationsService,
              private snackBar: MatSnackBar, protected musicPlayerService : MusicPlayerService,
              private isSubscribedService : IsSubscribedService,
              private dialog : MatDialog,
              private chatService : ChatService,) {
  }

  ngOnDestroy(): void {
    this.notificationsService.removeNotificationListener(this.notiCallback);
    this.chat$?.unsubscribe();
    //this.chat$?.unsubscribe();
  }

  ngOnInit(): void {
    if(!this.auth.isLoggedIn())
    {
      console.log("Not logged in...");
      setTimeout(() => this.router.navigate(['/auth/login'], {queryParams: {redirectUrl: "-listener"}}), 1000);
    }
    /*
    const request: IsSubscribedRequest = {
      userId : this.getUserIdFromToken()
    };
    this.isSubscribedService.handleAsync(request).subscribe({
      next: (response) => {
        if (!response.isSubscribed)
        {
          this.openPleaseSubscribeDialog();
        }
      },
      error: (err) => {
        console.error('Error:', err);
      },
    });

     */

    this.notificationsService.startConnection();
    this.notificationsService.addNotificationListener(this.notiCallback);

    this.chatService.startConnection();

    this.chat$ = this.chatService.msgNotify$.subscribe(this.notiCallback);
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
}
