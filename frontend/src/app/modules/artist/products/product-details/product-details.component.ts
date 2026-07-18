import { Component, OnInit } from '@angular/core';
import { Location } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProductGetByIdEndpointService, ProductGetResponse } from '../../../../endpoints/products-endpoints/produt-get-by-id-endpoint.service';
import { AddToShoppingCartEndpointService } from '../../../../endpoints/products-endpoints/add-to-shopping-cart-endpoint.service';
import { MyConfig } from '../../../../my-config';
import { CartUpdateService } from '../../../shared/shopping-cart/shopping-cart.service';
import { StorefrontWishlistService } from '../../../listener/web-store/storefront-wishlist.service';

@Component({
  selector: 'app-product-details',
  templateUrl: './product-details.component.html',
  styleUrls: ['./product-details.component.css'],
})
export class ProductDetailsComponent implements OnInit {
  product: ProductGetResponse | null = null;
  loading = true;
  errorMessage = '';
  currentSlide = 0;
  quantity = 1;
  private touchStartX = 0;
  readonly mediaAddress = MyConfig.media_address;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly location: Location,
    private readonly productService: ProductGetByIdEndpointService,
    private readonly addToShoppingCart: AddToShoppingCartEndpointService,
    private readonly router: Router,
    private readonly cart: CartUpdateService,
    private readonly wishlist: StorefrontWishlistService,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    const slug = this.route.snapshot.paramMap.get('slug');
    if (!slug) {
      this.loading = false;
      this.errorMessage = 'This product could not be found.';
      return;
    }

    this.wishlist.ensureLoaded().subscribe({ error: () => undefined });
    this.productService.handleAsync(slug).subscribe({
      next: product => {
        this.product = product;
        this.quantity = product.quantity > 0 ? 1 : 0;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'This product could not be loaded.';
        this.loading = false;
      },
    });
  }

  get wishlistSlugs$() {
    return this.wishlist.slugs$;
  }

  get currentArtwork(): string {
    const path = this.product?.photoPaths?.[this.currentSlide];
    return path ? this.mediaAddress + path : 'assets/icons/pattern.svg';
  }

  nextSlide(): void {
    const count = this.product?.photoPaths?.length ?? 0;
    if (count > 1) this.currentSlide = (this.currentSlide + 1) % count;
  }

  prevSlide(): void {
    const count = this.product?.photoPaths?.length ?? 0;
    if (count > 1) this.currentSlide = (this.currentSlide - 1 + count) % count;
  }

  changeSlide(index: number): void {
    this.currentSlide = index;
  }

  onTouchStart(event: TouchEvent): void {
    this.touchStartX = event.changedTouches[0]?.clientX ?? 0;
  }

  onTouchEnd(event: TouchEvent): void {
    const distance = (event.changedTouches[0]?.clientX ?? 0) - this.touchStartX;
    if (Math.abs(distance) < 45) return;
    distance > 0 ? this.prevSlide() : this.nextSlide();
  }

  changeQuantity(amount: number): void {
    if (!this.product?.quantity) return;
    this.quantity = Math.min(this.product.quantity, Math.max(1, this.quantity + amount));
  }

  addToCart(): void {
    if (!this.product || this.quantity < 1) return;
    const userId = this.getUserId();
    if (!userId) {
      this.snackBar.open('You must be logged in to add products to your cart.', 'Close', { duration: 2200 });
      return;
    }

    this.addToShoppingCart.handleAsync({ productId: this.product.id, userId, quantity: this.quantity }).subscribe({
      next: response => {
        if (response.success) {
          this.cart.notifyCartUpdated();
          this.snackBar.open('Added to cart', 'Close', { duration: 1600 });
        }
      },
      error: () => this.snackBar.open('Could not add this product to your cart.', 'Close', { duration: 2200 }),
    });
  }

  toggleWishlist(): void {
    if (!this.product) return;
    this.wishlist.toggle(this.product.slug).subscribe({
      next: added => this.snackBar.open(added ? 'Added to wishlist' : 'Removed from wishlist', 'Close', { duration: 1600 }),
      error: error => this.snackBar.open(error.message || 'Could not update wishlist', 'Close', { duration: 2200 }),
    });
  }

  goToArtistPage(): void {
    if (this.product?.artistId) this.router.navigate(['/listener/profile', this.product.artistId]);
  }

  goBack(): void {
    this.location.back();
  }

  private getUserId(): number {
    const token = sessionStorage.getItem('authToken') ?? localStorage.getItem('authToken');
    if (!token) return 0;
    try { return Number(JSON.parse(token).userId) || 0; } catch { return 0; }
  }
}
