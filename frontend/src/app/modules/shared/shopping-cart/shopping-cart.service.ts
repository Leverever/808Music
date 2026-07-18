import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, Subject, of, throwError } from 'rxjs';
import { catchError, finalize, map, shareReplay, switchMap, tap } from 'rxjs/operators';
import { GetShoppingCartService } from '../../../endpoints/products-endpoints/get-shopping-cart-endpoint.service';
import { RemoveFromShoppingCartService } from '../../../endpoints/products-endpoints/remove-item-from-shopping-cart-endpoint.service';
import { UpdateShoppingCartService } from '../../../endpoints/products-endpoints/update-shopping-cart-endpoint.service';

export interface StoreCartItem {
  productId: number;
  productTitle: string;
  productPhoto: string | null;
  quantity: number;
  price?: number;
  discountedPrice?: number;
  totalPrice: number;
}

@Injectable({ providedIn: 'root' })
export class CartUpdateService {
  private readonly cartUpdatedSource = new Subject<void>();
  private readonly itemsSubject = new BehaviorSubject<StoreCartItem[]>([]);
  private loadedUserId = 0;
  private loadRequest?: Observable<StoreCartItem[]>;

  readonly cartUpdated$ = this.cartUpdatedSource.asObservable();
  readonly items$ = this.itemsSubject.asObservable();
  readonly count$ = this.items$.pipe(map(items => items.reduce((count, item) => count + item.quantity, 0)));
  readonly total$ = this.items$.pipe(map(items => items.reduce((total, item) => total + item.totalPrice, 0)));

  constructor(
    private readonly getCart: GetShoppingCartService,
    private readonly updateCart: UpdateShoppingCartService,
    private readonly removeFromCart: RemoveFromShoppingCartService,
  ) {}

  load(force = false): Observable<StoreCartItem[]> {
    const userId = this.getUserId();
    if (!userId) {
      this.itemsSubject.next([]);
      return of([]);
    }

    if (!force && this.loadedUserId === userId) return of(this.itemsSubject.value);
    if (!force && this.loadRequest) return this.loadRequest;

    this.loadRequest = this.getCart.getCart(userId).pipe(
      map(response => (response.cartItems ?? []).map(raw => {
        const item = raw as any;
        const unitPrice = Number(item.discountedPrice ?? item.price ?? 0);
        return {
          ...item,
          totalPrice: Number(item.totalPrice ?? unitPrice * Number(item.quantity ?? 0)),
        } as StoreCartItem;
      })),
      tap(items => {
        this.loadedUserId = userId;
        this.itemsSubject.next(items);
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

  updateQuantity(productId: number, quantity: number): Observable<StoreCartItem[]> {
    const userId = this.getUserId();
    if (!userId || quantity < 1) return throwError(() => new Error('Invalid cart update.'));
    return this.updateCart.updateCart({ productId, userId, quantity }).pipe(switchMap(() => this.load(true)));
  }

  removeItem(productId: number): Observable<StoreCartItem[]> {
    const userId = this.getUserId();
    if (!userId) return throwError(() => new Error('You must be logged in.'));
    return this.removeFromCart.removeFromCart({ productId, userId }).pipe(switchMap(() => this.load(true)));
  }

  notifyCartUpdated(): void {
    this.loadedUserId = 0;
    this.cartUpdatedSource.next();
    this.load(true).subscribe({ error: () => undefined });
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
