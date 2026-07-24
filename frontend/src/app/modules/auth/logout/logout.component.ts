import {Component, OnInit} from '@angular/core';
import {HttpClient} from '@angular/common/http';
import {Router} from '@angular/router';
import {MyConfig} from '../../../my-config';
import {MyAuthService} from '../../../services/auth-services/my-auth.service';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';

@Component({
  selector: 'app-logout',
  templateUrl: './logout.component.html',
  styleUrls: ['../login/login.component.css', './logout.component.css']
})
export class LogoutComponent implements OnInit {
  private apiUrl = `${MyConfig.api_address}/auth/logout`;

  constructor(
    private authService: MyUserAuthService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.logout();
  }

  logout(): void {
    let info = this.authService.logOut();
    //alert(JSON.stringify(info));
    setTimeout(() => {
      this.router.navigate(['/auth/login']); // Preusmjeravanje na login nakon 3 sekunde
    }, 3000);
  }
}
