import {Component, Inject, inject} from '@angular/core';
import {MAT_BOTTOM_SHEET_DATA, MatBottomSheetRef} from '@angular/material/bottom-sheet';
import {MusicControllerComponent} from '../music-controller/music-controller.component';

export interface StemMixerBottomSheetData {
  controller: MusicControllerComponent;
}

@Component({
  selector: 'app-stem-mixer-bottom-sheet',
  templateUrl: './stem-mixer-bottom-sheet.component.html',
  styleUrl: './stem-mixer-bottom-sheet.component.css'
})
export class StemMixerBottomSheetComponent {
  private sheetRef =
    inject<MatBottomSheetRef<StemMixerBottomSheetComponent>>(MatBottomSheetRef);

  constructor(
    @Inject(MAT_BOTTOM_SHEET_DATA)
    protected data: StemMixerBottomSheetData
  ) {}

  dismiss(): void {
    this.sheetRef.dismiss();
  }
}
