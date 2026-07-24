import {HttpClient, HttpErrorResponse} from "@angular/common/http";
import {inject, Injectable} from "@angular/core";
import {LoginResponse} from '../../endpoints/auth-endpoints/user-auth-login-endpoint.service';
import {AuthTokenInfo} from './dto/auth-token-info';
import {jwtDecode} from "jwt-decode"
import {
  GetNewTokenRequest,
  UserGetNewTokenEndpointService
} from '../../endpoints/auth-endpoints/user-get-new-token-endpoint.service';
import {MyConfig} from '../../my-config';
import {tap} from 'rxjs/operators';
import {Observable} from 'rxjs';
import {LogoutInfo} from './dto/logout-info';
import {LogoutRequest} from './dto/logout-request';
import {Router} from '@angular/router';
import {MatDialog} from '@angular/material/dialog';
import {ConfirmDialogComponent} from '../../modules/shared/dialogs/confirm-dialog/confirm-dialog.component';

@Injectable({providedIn: 'root'})
export class MyUserAuthService {
  private static readonly dotNetRoleClaim =
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

  constructor(private httpClient: HttpClient,
              private router: Router,
              private myDialog: MatDialog,) {
    setInterval(() => {
      this.getAuthToken();
    },60000)
  }

  isLoggedIn(): boolean {
    return this.getAuthToken() != null;
  }

  isAdmin(): boolean {
    const token = this.getAuthToken(true)?.token;
    return token ? this.hasAdminRole(token) : false;
  }

  /*
  isManager(): boolean {
    return this.getMyAuthInfo()?.isManager ?? false;
  }
  */

  setLoggedInUser(lr: LoginResponse | null, rememberMe: boolean) {
    if(rememberMe) {
      if (lr == null) {
        window.localStorage.setItem("authToken", '');
      } else {
        window.localStorage.setItem("authToken", JSON.stringify(this.generateAuthTokenInfo(lr,rememberMe)));
      }
    }
    else {
      if (lr == null) {
        window.sessionStorage.setItem("authToken", '');
      } else {
        window.sessionStorage.setItem("authToken", JSON.stringify(this.generateAuthTokenInfo(lr,rememberMe)));
      }
    }
  }

  getAuthToken(dontRefresh: boolean = false): AuthTokenInfo | null {
    let tokenString = window.localStorage.getItem("authToken") ?? "";
    if(tokenString === "") {
      tokenString = window.sessionStorage.getItem("authToken") ?? "";
    }
    if(tokenString !== "")
    {
      let auth: AuthTokenInfo = JSON.parse(tokenString);
      //Decode and check if the jwt has expired
      let decodedJwt = jwtDecode(auth!.token);
      if(Date.now() + 5 * 60 * 1000 > decodedJwt.exp! * 1000 && !dontRefresh) {
        //if the caller doesn't want to refresh the token (ex. the http req interceptor)
        if(dontRefresh) {
          return null;
        }
        //tries to get a new jwt from the server with the refresh token
        this.getNewJwt(auth).subscribe({
          error: (err :HttpErrorResponse) => {
            alert(err.error);
            this.setLoggedInUser(null, auth.rememberMe);
            this.router.navigate(["/auth/login"]);
          }
        });
        if(Date.now() > decodedJwt.exp! * 1000)
        {
          this.router.navigate(["/please-wait-a-moment"]);
        }
      }
      return auth;
    }
    else {
      return null;
    }
  }

  public getNewJwt(auth: AuthTokenInfo): Observable<LoginResponse> {
    let url = `${MyConfig.api_address}/api/UserGetNewTokenEndpoint`;
    return this.httpClient.post<LoginResponse>(url,{
      jwtToken: auth.token,
      refreshToken: auth.refreshToken
    }).pipe(tap(response => {
      this.setLoggedInUser(response, auth.rememberMe);
    }));
  }

  private generateAuthTokenInfo(lr: LoginResponse, rememberMe: boolean) : AuthTokenInfo {
    let decodedJwt : any = jwtDecode(lr.token)
    return {
      userId: Number.parseInt(decodedJwt.sub!),
      email: decodedJwt.email,
      isAdmin: this.hasAdminRole(lr.token),
      token: lr.token,
      refreshToken: lr.refreshToken,
      rememberMe: rememberMe,
      username: decodedJwt.Username
    }
  }

  private hasAdminRole(token: string): boolean {
    try {
      const decodedJwt = jwtDecode<Record<string, unknown>>(token);
      const roleClaim =
        decodedJwt['role'] ??
        decodedJwt[MyUserAuthService.dotNetRoleClaim];
      const roles = Array.isArray(roleClaim) ? roleClaim : [roleClaim];

      return roles.some(role =>
        typeof role === 'string' && role.toLowerCase() === 'admin'
      );
    } catch {
      return false;
    }
  }

  public logOut():LogoutInfo {
    let url = `${MyConfig.api_address}/api/UserLogoutEndpoint`;
    let auth = this.getAuthToken();

    let logoutInfo : LogoutInfo = {
      requestSuccessful: false,
      serverMessage: "Not logged in"
    }

    if(auth !== null)
    {
      let logoutRequest: LogoutRequest = {
        jwtToken: auth?.token!,
        refreshToken: auth?.refreshToken!
      }
      this.httpClient.post(url, logoutRequest, {responseType:"text"}).subscribe({
        next: (data) => {
          logoutInfo.serverMessage = data;
          logoutInfo.requestSuccessful = true;
          this.setLoggedInUser(null, auth?.rememberMe!);
          window.sessionStorage.clear();
        },
        error: (err:HttpErrorResponse) => {
          logoutInfo.serverMessage = err.error;
          logoutInfo.requestSuccessful = false;
        }
      });

      //remove user stored data
      window.localStorage.removeItem("lang");
    }

    return logoutInfo;
  }
}
