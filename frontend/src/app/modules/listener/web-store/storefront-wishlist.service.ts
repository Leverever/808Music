import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { catchError, finalize, map, shareReplay, tap } from 'rxjs/operators';
import {
  GetWishlistEndpointService,
  WishlistItem,
} from '../../../endpoints/products-endpoints/get-wishlist-endpoint.service';
import { AddProductToWishlistEndpointService } from '../../../endpoints/products-endpoints/add-to-wishlist-endpoint.service';
import { RemoveProductFromWishlistService } from '../../../endpoints/products-endpoints/remove-item-from-wishlist-endpoint.service';

@Injectable({ providedIn: 'root' })
export class StorefrontWishlistService {
  private readonly slugsSubject = new BehaviorSubject<ReadonlySet<string>>(new Set<string>());
  private readonly itemsSubject = new BehaviorSubject<WishlistItem[]>([]);
  private loadedUserId = 0;
  private loadRequest?: Observable<WishlistItem[]>;

  readonly slugs$ = this.slugsSubject.asObservable();
  readonly items$ = this.itemsSubject.asObservable();

  constructor(
    private readonly getWishlist: GetWishlistEndpointService,
    private readonly addToWishlist: AddProductToWishlistEndpointService,
    private readonly removeFromWishlist: RemoveProductFromWishlistService,
  ) {}

  ensureLoaded(force = false): Observable<WishlistItem[]> {
    const userId = this.getUserId();
    if (!userId) {
      this.replaceItems([]);
      return of([]);
    }

    if (!force && this.loadedUserId === userId) {
      return of(this.itemsSubject.value);
    }

    if (!force && this.loadRequest) {
      return this.loadRequest;
    }

    this.loadRequest = this.getWishlist.handleAsync({ userId }).pipe(
      map(response => response.wishlistItems ?? []),
      tap(items => {
        this.loadedUserId = userId;
        this.replaceItems(items);
      }),
      catchError(error => {
        this.loadedUserId = 0;
        return throwError(() => error);
      }),
      finalize(() => this.loadRequest = undefined),
      shareReplay(1),
    );

    return this.loadRequest;
  }

  isWishlisted(slug: string): boolean {
    return this.slugsSubject.value.has(slug);
  }

  toggle(slug: string): Observable<boolean> {
    const userId = this.getUserId();
    if (!userId) {
      return throwError(() => new Error('You must be logged in to update your wishlist.'));
    }

    const removing = this.isWishlisted(slug);
    const request = removing
      ? this.removeFromWishlist.removeProductFromWishlist({ productSlug: slug, userId })
      : this.addToWishlist.handleAsync({ productSlug: slug, userId });

    return request.pipe(
      map(response => {
        if (!response.success) {
          throw new Error(response.message || 'Could not update wishlist.');
        }

        const next = new Set(this.slugsSubject.value);
        removing ? next.delete(slug) : next.add(slug);
        this.slugsSubject.next(next);
        if (removing) {
          this.itemsSubject.next(this.itemsSubject.value.filter(item => item.slug !== slug));
        } else {
          this.loadedUserId = 0;
        }
        return !removing;
      }),
    );
  }

  refresh(): Observable<WishlistItem[]> {
    return this.ensureLoaded(true);
  }

  private replaceItems(items: WishlistItem[]): void {
    this.itemsSubject.next(items);
    this.slugsSubject.next(new Set(items.map(item => item.slug)));
  }

  private getUserId(): number {
    const token = sessionStorage.getItem('authToken') ?? localStorage.getItem('authToken');
    if (!token) return 0;

    try {
      return Number(JSON.parse(token).userId) || 0;
    } catch {
      return 0;
    }
  }
}
