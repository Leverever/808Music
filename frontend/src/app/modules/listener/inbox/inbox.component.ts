import {Component, OnDestroy, OnInit} from '@angular/core';
import {ChatGetEndpointService} from '../../../endpoints/chat-endpoints/chat-get-endpoint.service';
import {ChatGetResponse} from '../../../endpoints/chat-endpoints/chat-create-endpoint.service';
import {MatBottomSheet} from '@angular/material/bottom-sheet';
import {
  CreateChatBottomSheetComponent
} from '../../shared/bottom-sheets/create-chat-bottom-sheet/create-chat-bottom-sheet.component';
import moment from 'moment';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';
import {ChatService, MsgSeen} from '../../../services/chat.service';
import {
  ChatGetMessagesEndpointService, MessageGetRequest,
  MessageGetResponse
} from '../../../endpoints/chat-endpoints/chat-get-messages-endpoint.service';
import {
  NotificationMarkAsReadEndpointService
} from '../../../endpoints/notification-endpoints/notification-mark-as-read-endpoint.service';
import {ChatMarkAsReadEndpointService} from '../../../endpoints/chat-endpoints/chat-mark-as-read-endpoint.service';
import {HttpErrorResponse} from '@angular/common/http';
import {MyConfig} from '../../../my-config';
import {MatChipListboxChange} from '@angular/material/chips';
import {ActivatedRoute} from '@angular/router';
import {Subscription} from 'rxjs';

@Component({
  selector: 'app-inbox',
  templateUrl: './inbox.component.html',
  styleUrl: './inbox.component.css'
})
export class InboxComponent implements OnInit, OnDestroy {
  chats : ChatGetResponse[] = [];
  queryString = "";
  selectedChat: ChatGetResponse | null = null;
  selectedChatMessages: MessageGetResponse[] = [];
  userId!: number;
  isMobileViewport = window.matchMedia('(max-width: 960px)').matches;
  protected readonly JSON = JSON;
  protected readonly moment = moment;

  chatReceive$!: Subscription;
  chatSeen$!: Subscription;
  chatBlocked$!: Subscription;

  showBlocked = false;

  messagePagedRequest : MessageGetRequest = {
    id: 0,
    pageNumber: 1,
    pageSize: 20
  }

  receiveMessageCallback = (msg: MessageGetResponse) => {
    if(this.selectedChat?.id === msg.userChatId)
    {
      this.selectedChatMessages.push(msg);
      this.markAsReadService.handleAsync({msgs:[msg]}).subscribe({
        next: value => {
          console.log("New message marked as read");
        }
      })
      if(this.userId !== msg.senderId)
      {
        let audio = new Audio("/assets/activeChatMsg.mp3");
        audio.volume = 0.15;
        audio.play();
      }
      this.shouldScroll = true;
    }
    else {
      let audio = new Audio("/assets/notification.mp3");
      audio.volume = 0.15;
      audio.play();
    }
    let userChat = this.chats.find(val => val.id === msg.userChatId);
    if(userChat && this.selectedChat?.id !== msg.userChatId && msg.senderId !== this.userId)
    {
      userChat.numberOfUnreads++;
    }

    userChat!.lastMessage = msg.message;
    userChat!.lastMessageAt = msg.sentAt;
    userChat!.lastMessageSenderId = msg.senderId;
  }

  msgSeenCallback  = (msg: MsgSeen) => {
    if(this.selectedChat?.id === msg.userChatId)
    {
      let updatemMsg : MessageGetResponse = this.selectedChatMessages.find(val => val.id === msg!.id)!;
      updatemMsg.seen = msg.seen;
      updatemMsg.seenAt = msg.seenAt;
    }
  }

  constructor(private getChatsService: ChatGetEndpointService,
              private matBtmSheet : MatBottomSheet,
              private chatService : ChatService,
              private getMessages : ChatGetMessagesEndpointService,
              private auth: MyUserAuthService,
              private markAsReadService : ChatMarkAsReadEndpointService,
              private route: ActivatedRoute) {
  }

  ngOnDestroy(): void {
    this.chatSeen$.unsubscribe();
    this.chatBlocked$.unsubscribe();
    this.chatReceive$.unsubscribe();
  }

  ngOnInit(): void {
    this.userId = this.auth.getAuthToken()!.userId;

    this.chatReceive$ = this.chatService.msgReceived$.subscribe(msg => {
      this.receiveMessageCallback(msg);
    })
    this.chatSeen$ = this.chatService.msgSeen$.subscribe(msg => {
      this.msgSeenCallback(msg);
    })

    this.getChatsService.handleAsync().subscribe({
      next: value => {
        this.chats = value;

        this.route.queryParams.subscribe(params => {
          let chatId = params['chat'];
          if(chatId)
          {
            let chat = this.chats.find(val => val.id === Number.parseInt(chatId, 10));
            if(chat !== undefined)
            {
              this.selectedChat = chat;
            }
            else {
              this.selectedChat = value[0] ?? null;
            }
          }
          else {
              this.selectedChat = this.isMobileViewport ? null : (value[0] ?? null);
          }

          if(this.selectedChat != null)
          {
            this.messagePagedRequest.id = this.selectedChat.id;
            this.fetchMessages();
          }
        })
      }
    })

    this.chatBlocked$ = this.chatService.chatBlocked$.subscribe(data => {
      let chat = this.chats.find(val => val.id === data.id);
      if(chat)
      {
        chat.blocked = data.isBlocked;
        chat.blockedByUser = data.blockedByUser;
        chat.blockedByUserId = data.blockedByUserId;
      }
    })


  }

  filterChats(queryString: string) {
    this.queryString = queryString;
  }

  openCreateSheet() {
    let ref = this.matBtmSheet.open(CreateChatBottomSheetComponent, {data:{chats: this.chats}});

    ref.afterDismissed().subscribe({
      next: value => {
        if(value)
        {
          this.chats.unshift(value as ChatGetResponse);
        }
      }
    });
  }

  openChat(c: ChatGetResponse) {
    this.selectedChat = c;
    this.messagePagedRequest.id = c.id;
    this.fetchMessages();
  }

  closeChatMobile(): void {
    this.selectedChat = null;
    this.selectedChatMessages = [];
  }

  fetchMessages() {
    this.messagePagedRequest.pageNumber = 1;
    this.getMessages.handleAsync(this.messagePagedRequest).subscribe({
      next: data => {
        this.selectedChatMessages = data.dataItems;
        this.markAsReadService.handleAsync({msgs: data.dataItems}).subscribe({
          next: value => {
            console.log("Marked as read");
          }
        })
        this.selectedChat!.numberOfUnreads = 0;
      }
    })
  }

  getFilteredChats() {
    let chats : ChatGetResponse[] = this.chats;
    if(this.queryString != "")
    {
      chats = chats.filter(c => c.chatter.toLowerCase().includes(this.queryString.toLowerCase()));
    }
    return chats.filter(c => c.blocked == this.showBlocked);
  }

  protected readonly Math = Math;
  shouldScroll: boolean = true;

  getUnreadCount(numberOfUnreads: number) {
    return numberOfUnreads > 99 ? "99+" : numberOfUnreads;
  }

  getLastMessage(lastMessage: ChatGetResponse) {
    return lastMessage.lastMessageSenderId == this.userId ? `You: ${lastMessage.lastMessage}` : lastMessage.lastMessage;
  }

  scroll() {
    this.shouldScroll = true;
  }

  protected readonly MyConfig = MyConfig;

  showHideBlocked($event: MatChipListboxChange) {
    this.showBlocked = !this.showBlocked;
    this.filterChats(this.queryString);
  }

  loadNewBatch(page: number) {
    this.messagePagedRequest.pageNumber = page;
    this.getMessages.handleAsync(this.messagePagedRequest).subscribe({
      next: data => {
        this.selectedChatMessages.unshift(...data.dataItems);
      }
    })
  }
}
