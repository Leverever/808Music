import { Component, HostListener, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MyConfig } from '../../../my-config';
import { CartUpdateService } from './shopping-cart.service';

@Component({
  selector: 'app-shopping-cart',
  templateUrl: './shopping-cart.component.html',
  styleUrls: ['./shopping-cart.component.css'],
})
export class ShoppingCartComponent implements OnInit {
  isCartVisible = false;
  readonly mediaAddress = MyConfig.media_address;

  constructor(
    readonly cart: CartUpdateService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.cart.load().subscribe({ error: () => undefined });
  }

  toggleCart(): void {
    this.isCartVisible = !this.isCartVisible;
    if (this.isCartVisible) this.cart.load().subscribe({ error: () => undefined });
  }

  updateQuantity(productId: number, quantity: number): void {
    this.cart.updateQuantity(productId, quantity).subscribe({ error: () => undefined });
  }

  removeItem(productId: number): void {
    this.cart.removeItem(productId).subscribe({ error: () => undefined });
  }

  proceedToCheckout(): void {
    this.isCartVisible = false;
    this.router.navigate(['/listener/checkout']);
  }

  @HostListener('document:keydown.escape')
  close(): void {
    this.isCartVisible = false;
  }
}
