import { Component, OnInit } from '@angular/core';
import { GetOrderResponse, OrderItem, OrderService } from '../../../endpoints/products-endpoints/orders-by-user-endpoint.service';
import { MyConfig } from '../../../my-config';

@Component({
  selector: 'app-order-list',
  templateUrl: './order-list.component.html',
  styleUrl: './order-list.component.css',
})
export class OrderListComponent implements OnInit {
  orders: OrderItem[] = [];
  userName = '';
  errorMessage: string | null = null;
  loading = true;
  readonly MyConfig = MyConfig;

  constructor(private readonly orderService: OrderService) {}

  ngOnInit(): void {
    const userId = this.getUserId();
    if (!userId) {
      this.loading = false;
      this.errorMessage = 'Sign in to view your orders.';
      return;
    }
    this.loadOrders(userId);
  }

  private loadOrders(userId: number): void {
    this.orderService.getOrdersByUser(userId).subscribe({
      next: (response: GetOrderResponse) => {
        if (response.success) {
          this.orders = response.orders ?? [];
          this.userName = response.userName;
          this.errorMessage = null;
        } else {
          this.errorMessage = response.message || 'Your orders could not be loaded.';
        }
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Your orders could not be loaded.';
        this.loading = false;
      },
    });
  }

  private getUserId(): number {
    const token = sessionStorage.getItem('authToken') ?? localStorage.getItem('authToken');
    if (!token) return 0;
    try { return Number(JSON.parse(token).userId) || 0; } catch { return 0; }
  }
}
