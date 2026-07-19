import {Component, Inject, inject, OnInit} from '@angular/core';
import {MAT_BOTTOM_SHEET_DATA, MatBottomSheetRef} from '@angular/material/bottom-sheet';
import {
  MyAppUserPreference
} from '../../../../endpoints/notification-endpoints/notification-set-preference-endpoint.service';
import {ChatCreateEndpointService, ChatGetResponse} from '../../../../endpoints/chat-endpoints/chat-create-endpoint.service';
import {
  UserSearchEndpointService,
  UserSearchResponse
} from '../../../../endpoints/user-endpoints/user-search-endpoint.service';
import {MyUserAuthService} from '../../../../services/auth-services/my-user-auth.service';
import {MyConfig} from '../../../../my-config';

@Component({
  selector: 'app-create-chat-bottom-sheet',
  templateUrl: './create-chat-bottom-sheet.component.html',
  styleUrls: ['./create-chat-bottom-sheet.component.css']
})
export class CreateChatBottomSheetComponent implements OnInit {
  private sheetRef = inject<MatBottomSheetRef<CreateChatBottomSheetComponent>>(MatBottomSheetRef);
  private searchString: string = "";
  users : UserSearchResponse[] = [];
  username: string = "";
  readonly mediaAddress = MyConfig.media_address;

  constructor(@Inject(MAT_BOTTOM_SHEET_DATA) protected data: {chats: ChatGetResponse[]},
              private chatCreateService : ChatCreateEndpointService,
              private userAutoComplete : UserSearchEndpointService,
              private auth : MyUserAuthService) {
  }

  ngOnInit(): void {
    this.findUsers("");
    this.username = this.auth.getAuthToken()!.username!;
  }

  private filterUsers(data: UserSearchResponse[]) {
    return data.filter((user: UserSearchResponse) =>
      this.data.chats.find(c => (c.chatter == user.username && c.otherChatter == this.username)
        || (c.chatter == this.username && c.otherChatter == user.username)) === undefined);
  }


  dismiss() {
    this.sheetRef.dismiss(null);
  }

  findUsers(searchString: string) {
    this.userAutoComplete.handleAsync({searchString: searchString}).subscribe({
      next: data => {
        this.users = this.filterUsers(data);
      }
    })
  }

  createChat(a: UserSearchResponse) {
    this.chatCreateService.handleAsync({secondaryUserId: a.id}).subscribe({
      next: data => {
        this.sheetRef.dismiss(data);
      }
    })
  }
}
