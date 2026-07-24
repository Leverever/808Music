import {Component, inject, Inject, OnInit} from '@angular/core';
import {MAT_BOTTOM_SHEET_DATA, MatBottomSheet, MatBottomSheetRef} from '@angular/material/bottom-sheet';
import {TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {MyConfig} from '../../../../my-config';
import {CdkDragDrop} from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-queue-view-bottom-sheet',
  templateUrl: './queue-view-bottom-sheet.component.html',
  styleUrl: './queue-view-bottom-sheet.component.css'
})
export class QueueViewBottomSheetComponent implements OnInit {
  private sheetRef = inject<MatBottomSheetRef<QueueViewBottomSheetComponent>>(MatBottomSheetRef);
  constructor(@Inject(MAT_BOTTOM_SHEET_DATA) protected data: {queue: TrackGetResponse[]},
              private musicPlayerService: MusicPlayerService) { }

  ngOnInit(): void {
    this.musicPlayerService.trackAdd.subscribe({next: data => {
      this.data.queue = this.musicPlayerService.getQueue();
    }})
  }

  removeSong(track: TrackGetResponse) {
    this.musicPlayerService.removeFromQueue(track);
    this.data.queue = this.musicPlayerService.getQueue();
  }

  protected readonly MyConfig = MyConfig;

  dismissSheet() {
    this.sheetRef.dismiss();
  }

  skipTo(track: TrackGetResponse) {
    this.musicPlayerService.skipTo(track);
    //this.removeSong(track);
    this.dismissSheet();
  }

  reorderQueue(event: CdkDragDrop<TrackGetResponse[]>): void {
    if(event.previousIndex === event.currentIndex)
    {
      return;
    }

    this.data.queue = this.musicPlayerService.reorderUpcomingQueue(
      event.previousIndex,
      event.currentIndex
    );
  }
}
