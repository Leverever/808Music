import {Injectable} from '@angular/core';
import {ActivatedRouteSnapshot, CanActivate, Router, UrlTree} from '@angular/router';
import {MyUserAuthService} from '../services/auth-services/my-user-auth.service';
import {MyAuthService} from '../services/auth-services/my-auth.service';

export class AuthGuardData {
  isAdmin?: boolean;
  isManager?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AuthGuard implements CanActivate {
  constructor(
    private authService: MyUserAuthService,
    private legacyAuthService: MyAuthService,
    private router: Router
  ) {
  }

  canActivate(route: ActivatedRouteSnapshot): boolean | UrlTree {
    const guardData = route.data as AuthGuardData;

    if (!this.authService.isLoggedIn()) {
      return this.router.createUrlTree(['/auth/login']);
    }

    if (guardData.isAdmin && !this.authService.isAdmin()) {
      return this.router.createUrlTree(['/unauthorized']);
    }

    if (guardData.isManager && !this.legacyAuthService.isManager()) {
      return this.router.createUrlTree(['/unauthorized']);
    }

    return true;
  }
}
