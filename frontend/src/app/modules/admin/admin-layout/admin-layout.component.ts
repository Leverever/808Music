import {Component} from '@angular/core';
import {Router} from '@angular/router';

@Component({
  selector: 'app-admin-layout',
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.css'
})
export class AdminLayoutComponent {
  mobileNavigationOpen = false;

  constructor(private router: Router) {
  }

  closeMobileNavigation(): void {
    this.mobileNavigationOpen = false;
  }

  returnToListener(): void {
    this.mobileNavigationOpen = false;
    this.router.navigate(['/listener/home']);
  }
}
