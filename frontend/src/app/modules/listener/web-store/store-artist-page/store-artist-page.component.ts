import { Component, OnInit } from '@angular/core';
import { ProductGetByArtistIdService, Product } from '../../../../endpoints/products-endpoints/product-get-by-artist-id.service';
import { Router, ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-store-artist-page',
  templateUrl: './store-artist-page.component.html',
  styleUrls: ['./store-artist-page.component.css']
})
export class StoreArtistPageComponent implements OnInit {
  Products: Product[] = [];
  loading: boolean = true;
  errorMessage: string = '';
  artistId: number = 1;

  constructor(
    private productService: ProductGetByArtistIdService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.artistId = +params['id'];
      this.loadProducts();
    });
  }

  viewProduct(slug: string): void {
    this.router.navigate(['listener/product', slug]);
  }

  loadProducts(): void {
    this.productService.getProductsByArtist(this.artistId).subscribe({
      next: (data: Product[]) => {
        this.Products = data
          .sort((a, b) => b.saleAmount - a.saleAmount);
        this.loading = false;
      },
      error: (err) => {
        this.errorMessage = 'Failed to load products';
        this.loading = false;
        console.error(err);
      }
    });
  }

}
