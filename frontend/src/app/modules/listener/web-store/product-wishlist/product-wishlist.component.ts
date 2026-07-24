import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { finalize } from 'rxjs/operators';
import { AddToShoppingCartEndpointService } from '../../../../endpoints/products-endpoints/add-to-shopping-cart-endpoint.service';
import { MyConfig } from '../../../../my-config';
import { CartUpdateService } from '../../../shared/shopping-cart/shopping-cart.service';
import { StorefrontWishlistService } from '../storefront-wishlist.service';

@Component({
  selector: 'app-product-wishlist',
  templateUrl: './product-wishlist.component.html',
  styleUrls: ['./product-wishlist.component.css'],
})
export class ProductWishlistComponent implements OnInit {
  loading = true;
  readonly mediaAddress = MyConfig.media_address;

  constructor(
    private readonly wishlist: StorefrontWishlistService,
    private readonly addToShoppingCart: AddToShoppingCartEndpointService,
    private readonly cart: CartUpdateService,
    private readonly snackBar: MatSnackBar,
    private readonly router: Router,
  ) {}

  get items$() {
    return this.wishlist.items$;
  }

  ngOnInit(): void {
    this.wishlist.ensureLoaded().pipe(finalize(() => this.loading = false)).subscribe({
      error: () => this.snackBar.open('Could not load your wishlist.', 'Close', { duration: 2200 }),
    });
  }

  openProduct(slug: string): void {
    this.router.navigate(['/listener/product', slug]);
  }

  removeFromWishlist(event: Event, slug: string): void {
    event.stopPropagation();
    this.wishlist.toggle(slug).subscribe({
      next: () => this.snackBar.open('Removed from wishlist', 'Close', { duration: 1600 }),
      error: () => this.snackBar.open('Could not update your wishlist.', 'Close', { duration: 2200 }),
    });
  }

  addToCart(event: Event, productId: number): void {
    event.stopPropagation();
    const userId = this.getUserId();
    if (!userId) {
      this.snackBar.open('You must be logged in to add products to your cart.', 'Close', { duration: 2200 });
      return;
    }

    this.addToShoppingCart.handleAsync({ productId, userId, quantity: 1 }).subscribe({
      next: response => {
        if (response.success) {
          this.cart.notifyCartUpdated();
          this.snackBar.open('Added to cart', 'Close', { duration: 1600 });
        }
      },
      error: () => this.snackBar.open('Could not add this product to your cart.', 'Close', { duration: 2200 }),
    });
  }

  discountPercent(sale: number): number {
    return Math.round((sale > 1 ? sale / 100 : sale) * 100);
  }

  private getUserId(): number {
    const token = sessionStorage.getItem('authToken') ?? localStorage.getItem('authToken');
    if (!token) return 0;
    try { return Number(JSON.parse(token).userId) || 0; } catch { return 0; }
  }
}
