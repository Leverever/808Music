import { Component, OnInit } from '@angular/core';
import { FormControl } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { debounceTime, switchMap } from 'rxjs/operators';
import { ProductsSearchService } from '../../../../endpoints/products-endpoints/search-by-title-endpoint.service';
import { ProductAutocompleteService } from '../../../../endpoints/products-endpoints/product-autocomplete-endpoint.service';
import { MyConfig } from '../../../../my-config';
import { Product } from '../product.model';

@Component({
  selector: 'app-store-search-results',
  templateUrl: './store-search.component.html',
  styleUrls: ['./store-search.component.css'],
})
export class StoreSearchComponent implements OnInit {
  searchResults: any[] = [];
  filteredProducts: Product[] = [];
  keyword = '';
  currentPage = 1;
  pageSize = 10;
  totalResults = 0;
  loading = true;
  noResults = false;
  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly mediaAddress = MyConfig.media_address;
  sortBy = 'datecreatednewest';
  selectedSortOption = 'dateCreatedNewest';
  readonly sortOptions = [
    { value: 'dateCreatedNewest', apiValue: 'datecreatednewest', label: 'Newest first' },
    { value: 'dateCreatedOldest', apiValue: 'datecreatedoldest', label: 'Oldest first' },
    { value: 'priceHighest', apiValue: 'discountedpricehighest', label: 'Highest price' },
    { value: 'priceLowest', apiValue: 'discountedpricelowest', label: 'Lowest price' },
    { value: 'saleHighest', apiValue: 'salehighest', label: 'Biggest discount' },
  ];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly productsSearch: ProductsSearchService,
    private readonly autocomplete: ProductAutocompleteService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      this.keyword = params['keyword'] || '';
      this.currentPage = Number(params['page']) || 1;
      this.sortBy = params['sortBy'] || 'datecreatednewest';
      this.selectedSortOption = this.sortOptions.find(option => option.apiValue === this.sortBy)?.value ?? 'dateCreatedNewest';
      this.searchControl.setValue(this.keyword, { emitEvent: false });
      this.fetchResults();
    });

    this.searchControl.valueChanges.pipe(
      debounceTime(250),
      switchMap(value => value.trim() ? this.autocomplete.handleAsync({ keyword: value.trim() }) : of([])),
    ).subscribe({ next: products => this.filteredProducts = products, error: () => this.filteredProducts = [] });
  }

  fetchResults(): void {
    this.loading = true;
    this.productsSearch.searchProducts(this.keyword, this.sortBy, this.currentPage, this.pageSize).subscribe({
      next: results => {
        this.searchResults = results.products ?? [];
        this.totalResults = results.total ?? 0;
        this.noResults = this.searchResults.length === 0;
        this.loading = false;
      },
      error: () => {
        this.searchResults = [];
        this.noResults = true;
        this.loading = false;
      },
    });
  }

  searchProducts(value = this.searchControl.value): void {
    const keyword = value.trim();
    if (keyword) {
      this.router.navigate(['/listener/product-search'], { queryParams: { keyword, page: 1, sortBy: this.sortBy } });
    }
  }

  viewProduct(slug: string): void {
    this.router.navigate(['/listener/product', slug]);
  }

  changeSortOrder(): void {
    this.sortBy = this.sortOptions.find(option => option.value === this.selectedSortOption)?.apiValue ?? 'datecreatednewest';
    this.currentPage = 1;
    this.updateUrl();
  }

  changePage(page: number): void {
    if (page < 1) return;
    this.currentPage = page;
    this.updateUrl();
  }

  private updateUrl(): void {
    this.router.navigate(['/listener/product-search'], {
      queryParams: { keyword: this.keyword, page: this.currentPage, sortBy: this.sortBy },
    });
  }
}
