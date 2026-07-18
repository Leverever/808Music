import { Component, OnInit } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { debounceTime, switchMap } from 'rxjs/operators';
import { ProductsGetTopWishlistedService } from '../../../../endpoints/products-endpoints/products-get-random-endpoint.service';
import { ProductsGetNewestService } from '../../../../endpoints/products-endpoints/product-get-newest-endpoint.service';
import { ProductsGetBestSellingService } from '../../../../endpoints/products-endpoints/product-get-best-selling-endpoint.service';
import { ProductsOnSaleService } from '../../../../endpoints/products-endpoints/products-on-sale-endpoint.service';
import { ProductAutocompleteService } from '../../../../endpoints/products-endpoints/product-autocomplete-endpoint.service';
import { MyConfig } from '../../../../my-config';
import { Product } from '../product.model';

@Component({
  selector: 'app-web-store',
  templateUrl: './store-home.component.html',
  styleUrls: ['./store-home.component.css'],
})
export class WebStoreComponent implements OnInit {
  bestSellingProducts: Product[] = [];
  randomProducts: Product[] = [];
  newestProducts: Product[] = [];
  onSaleProducts: Product[] = [];
  filteredProducts: Product[] = [];
  loading = true;
  errorMessage = '';
  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly mediaAddress = MyConfig.media_address;
  readonly logoAddress = MyConfig.api_address + '/media/webshop_logo.png';
  readonly categories = [
    { value: 0, label: 'Clothes', icon: 'checkroom' },
    { value: 1, label: 'Vinyls', icon: 'album' },
    { value: 2, label: 'CDs', icon: 'library_music' },
    { value: 3, label: 'Posters', icon: 'image' },
    { value: 4, label: 'Accessories', icon: 'watch' },
    { value: 5, label: 'More', icon: 'interests' },
  ];

  constructor(
    private readonly topWishlisted: ProductsGetTopWishlistedService,
    private readonly newest: ProductsGetNewestService,
    private readonly bestSelling: ProductsGetBestSellingService,
    private readonly onSale: ProductsOnSaleService,
    private readonly autocomplete: ProductAutocompleteService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.searchControl.valueChanges.pipe(
      debounceTime(250),
      switchMap(value => value.trim() ? this.autocomplete.handleAsync({ keyword: value.trim() }) : of([])),
    ).subscribe({
      next: products => this.filteredProducts = products,
      error: () => this.filteredProducts = [],
    });

    forkJoin([
      this.newest.handleAsync(),
      this.bestSelling.handleAsync(),
      this.topWishlisted.handleAsync(),
      this.onSale.handleAsync(),
    ]).subscribe({
      next: ([newest, bestSelling, explore, sale]) => {
        this.newestProducts = this.mapProducts(newest);
        this.bestSellingProducts = this.mapProducts(bestSelling);
        this.randomProducts = this.mapProducts(explore);
        this.onSaleProducts = this.mapProducts(sale);
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'The shop could not be loaded. Please try again.';
        this.loading = false;
      },
    });
  }

  searchProducts(value = this.searchControl.value): void {
    const keyword = value.trim();
    if (keyword) {
      this.router.navigate(['/listener/product-search'], { queryParams: { keyword } });
    }
  }

  viewProduct(slug: string): void {
    this.router.navigate(['/listener/product', slug]);
  }

  productPrice(product: Product): number {
    const sale = product.saleAmount > 1 ? product.saleAmount / 100 : product.saleAmount;
    return product.price * (1 - Math.max(0, sale));
  }

  private mapProducts(data: any[]): Product[] {
    return (data ?? []).map(item => ({ ...item }));
  }
}
