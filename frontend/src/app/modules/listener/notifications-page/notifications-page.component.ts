import {Component, inject, OnDestroy, OnInit} from '@angular/core';
import {MatChipSelectionChange} from '@angular/material/chips';
import {NotificationsService, RichNotification} from '../../../services/notifications.service';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';
import {
  NotificationGetAllEndpointService
} from '../../../endpoints/notification-endpoints/notification-get-all-endpoint.service';
import {Router} from '@angular/router';
import {
  NotificationGetTypesEndpointService
} from '../../../endpoints/notification-endpoints/notification-get-types-endpoint.service';
import {
  NotificationMarkAsReadEndpointService
} from '../../../endpoints/notification-endpoints/notification-mark-as-read-endpoint.service';
import {MatDialog} from '@angular/material/dialog';
import {
  DeleteConfirmationDialogComponent
} from '../../shared/delete-confirmation-dialog/delete-confirmation-dialog.component';
import {ConfirmDialogComponent} from '../../shared/dialogs/confirm-dialog/confirm-dialog.component';
import {
  NotificationGetPreferenceEndpointService
} from '../../../endpoints/notification-endpoints/notification-get-preference-endpoint.service';
import {MatBottomSheet} from '@angular/material/bottom-sheet';
import {
  UserPreferenceBottomSheetComponent
} from '../../shared/bottom-sheets/user-preference-bottom-sheet/user-preference-bottom-sheet.component';
import {map, Subscription} from 'rxjs';
import {MatSnackBar} from '@angular/material/snack-bar';
import {
  ManageFollowingBottomSheetComponent
} from '../../shared/bottom-sheets/manage-following-bottom-sheet/manage-following-bottom-sheet.component';
import {
  ChatMessageMarkAsReadEndpointService
} from '../../../endpoints/chat-endpoints/chat-message-mark-as-read-endpoint.service';
import {ChatService} from '../../../services/chat.service';

@Component({
  selector: 'app-notifications-page',
  templateUrl: './notifications-page.component.html',
  styleUrl: './notifications-page.component.css'
})
export class NotificationsPageComponent implements OnInit, OnDestroy {
  notifications: RichNotification[] = [];
  filteredNotifications : RichNotification[] = [];
  notiTypes : string[] = [];
  filterText: string = 'All';
  matDialog = inject(MatDialog);
  notiCallback = (noti:RichNotification) => {
    this.notifications.unshift(noti);
    this.filteredNotifications = this.notifications;
    this.filterNotis();
  }
  priotity: number = 0;
  chat$! : Subscription;

  constructor(private notificationsService: NotificationsService,
              private auth: MyUserAuthService,
              private notisGetService: NotificationGetAllEndpointService,
              private router: Router,
              private getNotiTypesService : NotificationGetTypesEndpointService,
              private markAsReadService : NotificationMarkAsReadEndpointService,
              private getPreferenceService : NotificationGetPreferenceEndpointService,
              private bottomSheet : MatBottomSheet,
              private snackBar : MatSnackBar,
              private msgMarkAsRead: ChatMessageMarkAsReadEndpointService,
              private chatService: ChatService) {}

  ngOnDestroy(): void {
    this.notificationsService.removeNotificationListener(this.notiCallback);
    this.chat$.unsubscribe();
  }

  ngOnInit(): void {
      this.reloadData();
      this.getNotiTypesService.handleAsync().subscribe({
        next: data => {
          this.notiTypes = data;
        }
      });

    this.notificationsService.addNotificationListener(this.notiCallback);
    this.chat$ = this.chatService.msgNotify$.subscribe(this.notiCallback);
  }

  filterSelectedType(e: MatChipSelectionChange) {
    if(e.selected && e.isUserInput)
    {
      this.filterText = e.source.value as string;
    }
    else {
      this.filterText = "All";
    }
    this.filterNotis();
  }

  filterNotis() {
    this.filteredNotifications = this.notifications.filter(val => {
      if(this.filterText !== "All")
      {
       return val.type === this.filterText
      }
      else  {
        return true;
      }
    }
    );
    this.filteredNotifications = this.filteredNotifications.filter(val => {
      if(this.priotity !== 0)
      {
        return (val.priority && this.priotity == 2) || (!val.priority && this.priotity == 1);
      }
      else  {
        return true;
      }
    });

    if(this.filteredNotifications.length <= 0)
    {
      this.snackBar.open("No notifications found for the applied filters.", "Dismiss", {duration: 2000});
    }
  }

  go() {
    this.router.navigate(["/listener/home"]);
  }

  private reloadData() {
    this.notisGetService.handleAsync().subscribe({
      next: data => {
        this.notifications = data;
        this.filteredNotifications = data;
      }
    })
  }

  removeNoti(noti: RichNotification | null) {
    this.reloadData()
  }

  clearAllNotis() {
    let matRef = this.matDialog.open(ConfirmDialogComponent,
      {data: {title: "Are you sure you want to clear all notifications", content: "This action is irreversible!"}, hasBackdrop: true});
    matRef.afterClosed().subscribe({next: data => {
      if(data)
      {
        for (const n of this.notifications) {
          if(n.type !== "Message")
          {
            this.markAsReadService.handleAsync(n.id).subscribe({
              next: data => {
                this.removeNoti(n);
            }})
          }
          else {
            this.msgMarkAsRead.handleAsync({messageId: n.id}).subscribe({
              next: data => {
                this.removeNoti(n);
              }
            })
          }
        }
      }
    }});
  }

  openSettings() {
    let btmRef = this.bottomSheet.open(UserPreferenceBottomSheetComponent, {
      hasBackdrop: true,
      panelClass: ['liquid-glass-sheet-pane', 'listener-content-sheet-pane'],
      backdropClass: 'liquid-glass-sheet-backdrop',
    });
    btmRef.afterDismissed().subscribe({
      next: data => {
        if(data)
        {
          this.reloadData();
        }
      }})
  }

  filterPriority(e: MatChipSelectionChange) {
    if(e.selected && e.isUserInput)
    {
      this.priotity = Number.parseInt(e.source.id);
    }
    else {
      this.priotity = 0;
    }
    this.filterNotis();
  }

  openManageFollowing() {
    this.bottomSheet.open(ManageFollowingBottomSheetComponent, {
      hasBackdrop: true,
      panelClass: ['liquid-glass-sheet-pane', 'listener-content-sheet-pane'],
      backdropClass: 'liquid-glass-sheet-backdrop',
    });
  }
}
