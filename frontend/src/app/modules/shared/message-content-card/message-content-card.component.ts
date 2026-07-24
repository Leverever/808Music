import {Component, EventEmitter, Input, Output} from '@angular/core';
import {Router} from '@angular/router';

export interface MessageContent {
  name: string,
  type: string,
  picture: string,
  id: number,
}

@Component({
  selector: 'app-message-content-card',
  templateUrl: './message-content-card.component.html',
  styleUrls: ['./message-content-card.component.css']
})
export class MessageContentCardComponent {
  @Input("content") a : MessageContent | null = null;
  @Output() onButtonClick: EventEmitter<MessageContent | null> = new EventEmitter();
  @Input() btnIconName: string = "play_arrow";
  @Input() iconButton: boolean = false;
  @Input() btnLabel: string = "Attach";
  @Input() alignTextEnd = false;

  constructor(private router: Router) {
  }


  attachContent(a: MessageContent | null) {
    this.onButtonClick.emit(a);
  }

  goToContent() {
    switch (this.a?.type)
    {
      case "Album": this.router.navigate(["/listener/release", this.a!.id]); break;
      case "Track": this.router.navigate(["/listener/track", this.a!.id]); break;
      case "Artist": this.router.navigate(["/listener/profile", this.a!.id]); break;
    }
  }
}
