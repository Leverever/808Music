import {Component, Inject, inject, OnInit} from '@angular/core';
import {MAT_BOTTOM_SHEET_DATA, MatBottomSheetRef} from '@angular/material/bottom-sheet';
import {ChatGetResponse} from '../../../../endpoints/chat-endpoints/chat-create-endpoint.service';
import {TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {ChatGetEndpointService} from '../../../../endpoints/chat-endpoints/chat-get-endpoint.service';
import {sendMessage} from '@microsoft/signalr/dist/esm/Utils';
import {MyConfig} from '../../../../my-config';
import {MatCheckboxChange} from '@angular/material/checkbox';
import {ChatService} from '../../../../services/chat.service';
import {FormControl} from '@angular/forms';
import {MatSnackBar} from '@angular/material/snack-bar';

@Component({
  selector: 'app-send-song-message',
  templateUrl: './send-song-message.component.html',
  styleUrls: ['../../search-page/search-page.component.css','../../../artist/manage-users/manage-users.component.css','../manage-following-bottom-sheet/manage-following-bottom-sheet.component.css','../create-chat-bottom-sheet/create-chat-bottom-sheet.component.css', './send-song-message.component.css']
})
export class SendSongMessageComponent implements OnInit {
  sheetRef = inject<MatBottomSheetRef<SendSongMessageComponent>>(MatBottomSheetRef);
  chats: ChatGetResponse[] = [];
  message = new FormControl("");

  chatsToSendTo : ChatGetResponse[] = [];

  queryString = "";

  constructor(@Inject(MAT_BOTTOM_SHEET_DATA) protected data: {track: TrackGetResponse},
              private chatsGet: ChatGetEndpointService,
              private chatService: ChatService,
              private snackBar : MatSnackBar) {
  }

  ngOnInit(): void {
    this.chatsGet.handleAsync().subscribe((data) => {
      this.chats = data;
    })
  }

  findChats(string: string) {
    this.queryString = string ?? "";
    this.getChats();
  }

  dismiss() {
    this.sheetRef.dismiss();
  }

  getChats() {
    let chats = this.chats;
    if(this.queryString !== "") {
      chats = chats.filter(c => c.chatter.toLowerCase().includes(this.queryString.toLowerCase()));
    }
    return chats.filter(c => !c.blocked);
  }

  protected readonly sendMessage = sendMessage;

  sendMessages() {
    for (let c of this.chatsToSendTo)
    {
      this.chatService.sendMessage({
        message: this.message.value === "" ? "Shared a song" : this.message.value!,
        userChatId: c.id,
        contentType: "Track",
        contentId: this.data.track.id,
      }).then(value => {
        if(value === 200)
        {
          this.snackBar.open("Messages sent.", "Dismiss", {duration: 2000});
          this.sheetRef.dismiss(true);
        }
        else
          this.snackBar.open("Failed to send messages.", "Dismiss", {duration: 2000});
          this.sheetRef.dismiss(false);
      }).catch(error => {
        this.snackBar.open("Failed to send messages.", "Dismiss", {duration: 2000});
        this.sheetRef.dismiss(false);
      })
    }
  }

  protected readonly MyConfig = MyConfig;

  checkChat(event: MatCheckboxChange, a: ChatGetResponse) {
    this.setChatSelected(a, event.checked);
  }

  toggleChat(chat: ChatGetResponse): void {
    this.setChatSelected(chat, !this.checkIfSelected(chat));
  }

  private setChatSelected(chat: ChatGetResponse, selected: boolean): void {
    if(selected)
    {
      if(!this.checkIfSelected(chat))
      {
        this.chatsToSendTo.push(chat);
      }
    }
    else
    {
      this.chatsToSendTo = this.chatsToSendTo.filter(c => c.id !== chat.id);
    }
  }

  checkIfSelected(a: ChatGetResponse) {
    return this.chatsToSendTo.includes(a)
  }
}
