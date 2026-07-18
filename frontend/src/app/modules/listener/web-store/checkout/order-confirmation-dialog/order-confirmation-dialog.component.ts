import { Component, Inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Router } from '@angular/router';

@Component({
  selector: 'app-order-confirmation-dialog',
  templateUrl: './order-confirmation-dialog.component.html',
  styleUrls: ['./order-confirmation-dialog.component.css'],
})
export class OrderConfirmationDialogComponent {
  readonly orderCode: string;

  constructor(
    private readonly dialogRef: MatDialogRef<OrderConfirmationDialogComponent>,
    @Inject(MAT_DIALOG_DATA) data: { orderCode: string },
    private readonly router: Router,
  ) {
    this.orderCode = data?.orderCode ?? '';
  }

  onBackToHome(): void {
    this.dialogRef.close();
    this.router.navigate(['/listener/home']);
  }

  onBackToStore(): void {
    this.dialogRef.close();
    this.router.navigate(['/listener/store-home']);
  }
}
