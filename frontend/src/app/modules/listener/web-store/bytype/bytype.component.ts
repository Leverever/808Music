import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ProductByTypeService } from '../../../../endpoints/products-endpoints/products-by-type-endpoint.service';

@Component({
  selector: 'app-products-by-type',
  templateUrl: './bytype.component.html',
  styleUrls: ['./bytype.component.css'],
})
export class BytypeComponent implements OnInit {
  searchResults: any[] = [];
  productType: number | null = null;
  currentPage = 1;
  pageSize = 10;
  totalResults = 0;
  loading = true;
  noResults = false;
  sortBy = 'datecreatednewest';
  selectedSortOption = 'dateCreatedNewest';
  readonly sortOptions = [
    { value: 'dateCreatedNewest', apiValue: 'datecreatednewest', label: 'Newest first' },
    { value: 'dateCreatedOldest', apiValue: 'datecreatedoldest', label: 'Oldest first' },
    { value: 'priceHighest', apiValue: 'discountedpricehighest', label: 'Highest price' },
    { value: 'priceLowest', apiValue: 'discountedpricelowest', label: 'Lowest price' },
    { value: 'saleHighest', apiValue: 'salehighest', label: 'Biggest discount' },
  ];
  readonly productTypes = [
    { value: 0, label: 'Clothes' },
    { value: 1, label: 'Vinyls' },
    { value: 2, label: 'CDs' },
    { value: 3, label: 'Posters' },
    { value: 4, label: 'Accessories' },
    { value: 5, label: 'Miscellaneous' },
  ];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly productByType: ProductByTypeService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      this.productType = params['productType'] !== undefined ? Number(params['productType']) : null;
      this.currentPage = Number(params['page']) || 1;
      this.sortBy = params['sortBy'] || 'datecreatednewest';
      this.selectedSortOption = this.sortOptions.find(option => option.apiValue === this.sortBy)?.value ?? 'dateCreatedNewest';
      this.fetchResults();
    });
  }

  fetchResults(): void {
    if (this.productType === null) {
      this.searchResults = [];
      this.loading = false;
      return;
    }

    this.loading = true;
    this.productByType.getProductsByType(this.productType, this.currentPage, this.pageSize, this.sortBy).subscribe({
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

  get categoryLabel(): string {
    return this.productTypes.find(type => type.value === this.productType)?.label ?? 'Products';
  }

  changeCategory(): void {
    this.currentPage = 1;
    this.updateUrl();
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
    this.router.navigate(['/listener/product-type'], {
      queryParams: { productType: this.productType, page: this.currentPage, sortBy: this.sortBy },
    });
  }
}
