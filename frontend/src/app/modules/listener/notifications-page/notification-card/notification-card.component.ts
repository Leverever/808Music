import {AfterViewInit, ChangeDetectorRef, Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {RichNotification} from '../../../../services/notifications.service';
import {MyConfig} from '../../../../my-config';
import moment from 'moment';
import {Router} from '@angular/router';
import {TrackGetAllEndpointService} from '../../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {
  NotificationMarkAsReadEndpointService
} from '../../../../endpoints/notification-endpoints/notification-mark-as-read-endpoint.service';
import {ChatMarkAsReadEndpointService} from '../../../../endpoints/chat-endpoints/chat-mark-as-read-endpoint.service';
import {MatSnackBar} from '@angular/material/snack-bar';
import {
  ChatMessageMarkAsReadEndpointService
} from '../../../../endpoints/chat-endpoints/chat-message-mark-as-read-endpoint.service';

@Component({
  selector: 'app-notification-card',
  templateUrl: './notification-card.component.html',
  styleUrl: './notification-card.component.css'
})
export class NotificationCardComponent implements AfterViewInit {
  @Input() notification: RichNotification | null = null;
  @Output() onRead: EventEmitter<RichNotification | null> = new EventEmitter();
  thumbnail: { [p: string]: any } | null | undefined;

  constructor(private router: Router,
              private tracksGetService : TrackGetAllEndpointService,
              private musicPlayerService: MusicPlayerService,
              private markAsReadService: NotificationMarkAsReadEndpointService,
              private cd : ChangeDetectorRef,
              private msgMarkAsRead : ChatMessageMarkAsReadEndpointService,
              private snackBar: MatSnackBar,) {
  }


  ngAfterViewInit(): void {
      this.thumbnail = {
        'background-image': `url(${MyConfig.api_address}${this.notification?.imageUrl})`,
        'border-radius': this.notification?.type === "Message" ? "50%" : "15px"
      }
      this.cd.detectChanges();
  }

  getTimeFromNow() : string{
    if(this.notification)
    {
      return moment(this.notification.createdAt).fromNow();
    }
    return "";
  }

  emitReadNotification() {
    this.onRead.emit(this.notification ?? null);
  }

  goToArtist() {
    this.router.navigate(["/listener/profile", this.notification?.artist?.id]);
  }

  goToContent() {
    switch (this.notification?.type) {
      case "Album":
        this.router.navigate(["listener/release/"+this.notification?.contentId]);
        break;
      case "Product":
        this.router.navigate(["listener/product/"+this.notification?.slug]);
        break;
      case "Message":
        this.router.navigate(["listener/chat/"], {queryParams: {chat: this.notification?.contentId}});
        break;
      default: break;
    }
  }

  processContent() {
    switch (this.notification?.type) {
      case "Album":
        this.tracksGetService.handleAsync({pageSize: 1000,
          pageNumber: 1,
          albumId: this.notification?.contentId,
          isReleased: true
        }).subscribe({
          next: data => {
            if(data.dataItems.length > 0)
            {
              this.musicPlayerService.createQueue(data.dataItems, {display: this.notification?.title ?? "",
                value: "/listener/release/"+this.notification?.contentId})
            }
          }
        });
        break;
      default: this.goToContent(); break;
    }
  }

  markAsRead() {
    if(this.notification)
    {
      if(this.notification.type !== "Message")
      {
      this.markAsReadService.handleAsync(this.notification?.id).subscribe({
        next: data => {
            this.snackBar.open("Marked as read.", "Dismiss", {duration: 1000});
            this.emitReadNotification();
        }});
      }
      else {
        this.msgMarkAsRead.handleAsync({messageId: this.notification.id}).subscribe({
          next: data => {
            this.snackBar.open("Marked as read.", "Dismiss", {duration: 1000});
            this.emitReadNotification();
          }
        })
      }
    }
  }

  protected readonly MyConfig = MyConfig;
}
