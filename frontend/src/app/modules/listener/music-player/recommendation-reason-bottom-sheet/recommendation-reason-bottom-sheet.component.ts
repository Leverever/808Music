import {Component, Inject} from '@angular/core';
import {MAT_BOTTOM_SHEET_DATA, MatBottomSheetRef} from '@angular/material/bottom-sheet';

export interface RecommendationReasonBottomSheetData {
  reason: string;
}

@Component({
  selector: 'app-recommendation-reason-bottom-sheet',
  templateUrl: './recommendation-reason-bottom-sheet.component.html',
  styleUrl: './recommendation-reason-bottom-sheet.component.css'
})
export class RecommendationReasonBottomSheetComponent {
  constructor(
    @Inject(MAT_BOTTOM_SHEET_DATA) readonly data: RecommendationReasonBottomSheetData,
    private readonly sheetRef: MatBottomSheetRef<RecommendationReasonBottomSheetComponent>
  ) {}

  dismiss(): void {
    this.sheetRef.dismiss();
  }
}
