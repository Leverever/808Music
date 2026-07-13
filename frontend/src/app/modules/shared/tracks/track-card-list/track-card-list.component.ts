import {Component, EventEmitter, Input, Output} from '@angular/core';
import {TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';

@Component({
  selector: 'app-track-card-list',
  templateUrl: './track-card-list.component.html',
  styleUrls: ['../../../listener/artist-page/artist-music-page/artist-music-page.component.css','../../artist/artist-big-card-list/artist-big-card-list.component.css','./track-card-list.component.css']
})
export class TrackCardListComponent {
  @Input() tracks : TrackGetResponse[] = [];
  @Input() artistMode: boolean = false;
  @Input() title: string = "Songs";
  @Input() maxItems: number = 4;
  @Input() showPlayAll: boolean = false;
  @Input() useCustomPlayHandler: boolean = false;
  @Output() playAll = new EventEmitter<void>();
  @Output() trackSelected = new EventEmitter<number>();

  get cardLimit(): number {
    return this.artistMode ? 8 : this.maxItems;
  }
}
