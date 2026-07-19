import {AfterViewInit, ChangeDetectorRef, Component, Input, OnInit} from '@angular/core';
import {MessageGetResponse} from '../../../../endpoints/chat-endpoints/chat-get-messages-endpoint.service';
import {MessageContent} from '../../../shared/message-content-card/message-content-card.component';
import moment from 'moment';
import {MyUserAuthService} from '../../../../services/auth-services/my-user-auth.service';
import {ChatService} from '../../../../services/chat.service';
import {AlbumGetByIdEndpointService} from '../../../../endpoints/album-endpoints/album-get-by-id-endpoint.service';
import {TrackGetByIdEndpointService} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {MyConfig} from '../../../../my-config';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {ArtistGetByIdEndpointService} from '../../../../endpoints/artist-endpoints/artist-get-by-id-endpoint.service';
import {
  TrackGetAllEndpointService,
  TrackGetAllRequest
} from '../../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {MatSnackBar} from '@angular/material/snack-bar';

@Component({
  selector: 'app-message-card',
  templateUrl: './message-card.component.html',
  styleUrl: './message-card.component.css'
})
export class MessageCardComponent implements OnInit, AfterViewInit {
  @Input() message: MessageGetResponse | null = null;
  @Input() showStatusByDefault = false;
  userId: number = -1;
  statusRevealed = false;
  private holdTimer: number | null = null;
  private holdStartX = 0;
  private holdStartY = 0;
  result : MessageContent | null = null;

  constructor(private auth: MyUserAuthService,
              private changeDetectorRef: ChangeDetectorRef,
              private albumGetService: AlbumGetByIdEndpointService,
              private trackGetService: TrackGetByIdEndpointService,
              private artistGetByIdService: ArtistGetByIdEndpointService,
              private trackGetAllService: TrackGetAllEndpointService,
              private musicPlayerService: MusicPlayerService,
              private snackBar: MatSnackBar) {

  }

  ngAfterViewInit(): void {
    switch (this.message?.contentType)
    {
      case "Album": this.albumGetService.handleAsync(this.message?.contentId).subscribe(data => {
        this.result = {
          picture: `${MyConfig.api_address}${data.coverPath}`,
          name: data.title,
          type: "Album",
          id: data.id
        }
      }); break;
      case "Track": this.trackGetService.handleAsync(this.message?.contentId).subscribe(data => {
        this.result = {
          picture: `${MyConfig.api_address}${data.coverPath}`,
          name: data.title,
          type: "Track",
          id: data.id
        }
      }); break;
      case "Artist": this.artistGetByIdService.handleAsync(this.message?.contentId).subscribe(data => {
        this.result = {
          picture: `${MyConfig.api_address}${data.profilePhotoPath}`,
          name: data.name,
          type: "Artist",
          id: data.id
        }
      }); break;
    }

    this.changeDetectorRef.detectChanges();

  }


  ngOnInit(): void {
    this.userId = this.auth.getAuthToken()!.userId;
  }

  playContent(msg: MessageContent | null) {
    let pagedRequest : TrackGetAllRequest = {
      pageSize: 100000,
      pageNumber: 1,
      isReleased: true,
    }
    switch (msg?.type) {
      case "Album":
        pagedRequest.albumId = msg.id;
        this.trackGetAllService.handleAsync(pagedRequest).subscribe({
          next: value => {
            if(value.dataItems.length > 0)
            {
              this.musicPlayerService.createQueue(value.dataItems, {display: "From messages", value:"/listener/chat"})
            }
            else
            {
              this.snackBar.open("No songs found...", "Dismiss", {duration: 2000});
            }
          }
        });
        break;
      case "Track":
        this.trackGetService.handleAsync(msg?.id).subscribe({
          next: value => {
            this.musicPlayerService.createQueue([value], {display: "From messages", value:"/listener/chat"})
          }
        });
        break;
      case "Artist":
        pagedRequest.leadArtistId = msg.id;
        pagedRequest.sortByStreams = true;
        this.trackGetAllService.handleAsync(pagedRequest).subscribe({
          next: value => {
            if(value.dataItems.length > 0)
            {
              this.musicPlayerService.createQueue(value.dataItems, {display: "From messages", value:"/listener/chat"})
            }
            else
            {
              this.snackBar.open("No songs found...", "Dismiss", {duration: 2000});
            }
          }
        });
        break;
    }
  }

  getSeenInfo() {
    return this.message?.seen ? `Seen ${moment(this.message?.seenAt).fromNow()}` : `Sent ${moment(this.message?.sentAt).fromNow()}`;
  }

  startStatusHold(event: PointerEvent): void {
    if (this.showStatusByDefault || event.button !== 0) return;
    if ((event.target as HTMLElement).closest('button, a')) return;

    this.endStatusHold();
    this.holdStartX = event.clientX;
    this.holdStartY = event.clientY;
    this.holdTimer = window.setTimeout(() => {
      this.statusRevealed = !this.statusRevealed;
      this.holdTimer = null;
    }, 460);
  }

  moveStatusHold(event: PointerEvent): void {
    if (this.holdTimer === null) return;
    if (Math.hypot(event.clientX - this.holdStartX, event.clientY - this.holdStartY) > 9) {
      this.endStatusHold();
    }
  }

  endStatusHold(): void {
    if (this.holdTimer !== null) {
      window.clearTimeout(this.holdTimer);
      this.holdTimer = null;
    }
  }

  suppressContextMenu(event: MouseEvent): void {
    if (!this.showStatusByDefault) event.preventDefault();
  }

  protected readonly moment = moment;

}
