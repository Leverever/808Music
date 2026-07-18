import { Component, Input, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MyConfig } from '../../../../my-config';
import { StorefrontWishlistService } from '../storefront-wishlist.service';

@Component({
  selector: 'app-store-product-card',
  templateUrl: './store-product-card.component.html',
  styleUrls: ['./store-product-card.component.css'],
})
export class StoreProductCardComponent implements OnInit {
  @Input({ required: true }) product!: any;
  @Input() showWishlist = true;
  @Input() compact = false;

  readonly mediaAddress = MyConfig.media_address;

  constructor(
    private readonly router: Router,
    private readonly wishlist: StorefrontWishlistService,
    private readonly snackBar: MatSnackBar,
  ) {}

  get wishlistSlugs$() {
    return this.wishlist.slugs$;
  }

  ngOnInit(): void {
    this.wishlist.ensureLoaded().subscribe({ error: () => undefined });
  }

  openProduct(): void {
    if (this.product?.slug) {
      this.router.navigate(['/listener/product', this.product.slug]);
    }
  }

  toggleWishlist(event: Event): void {
    event.stopPropagation();
    this.wishlist.toggle(this.product.slug).subscribe({
      next: added => this.snackBar.open(
        added ? 'Added to wishlist' : 'Removed from wishlist',
        'Close',
        { duration: 1600 },
      ),
      error: error => this.snackBar.open(error.message || 'Could not update wishlist', 'Close', { duration: 2200 }),
    });
  }

  imagePath(): string {
    const photos = this.product?.photoPaths;
    const path = Array.isArray(photos) && photos.length
      ? photos[photos.length - 1]
      : this.product?.productPhoto;
    return path ? this.mediaAddress + path : 'assets/icons/pattern.svg';
  }

  currentPrice(): number {
    if (Number.isFinite(this.product?.discountedPrice)) {
      return Number(this.product.discountedPrice);
    }
    const price = Number(this.product?.price ?? this.product?.originalPrice ?? 0);
    const sale = this.saleFraction();
    return price * (1 - sale);
  }

  originalPrice(): number {
    return Number(this.product?.price ?? this.product?.originalPrice ?? this.currentPrice());
  }

  saleFraction(): number {
    const sale = Number(this.product?.saleAmount ?? 0);
    return sale > 1 ? sale / 100 : Math.max(0, sale);
  }

  salePercent(): number {
    return Math.round(this.saleFraction() * 100);
  }
}
