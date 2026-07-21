import {Component, OnInit} from '@angular/core';
import {
  PasswordResetRequest,
  UserPasswordResetEndpointService
} from '../../../endpoints/auth-endpoints/user-password-reset-endpoint.service';
import {ActivatedRoute, Router} from '@angular/router';

@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.css', '../login/login.component.css', '../forget-password/forget-password.component.css']
})
export class ResetPasswordComponent implements OnInit {
  resetRequest: PasswordResetRequest = {
    password: "",
    resetToken: ""
  }
  repeatPassword: string = "";

  sentRequest: boolean = false;
  successText: string = "";
  errorMessage: string | null = null;

  constructor(private passResetService: UserPasswordResetEndpointService, private router: Router, private route: ActivatedRoute) {

  }

  ngOnInit(): void {
        this.route.queryParams.subscribe(param => {
          this.resetRequest.resetToken = param['resetToken'] ?? "";
          if(this.resetRequest.resetToken === "")
          {
            alert("Token is invalid.");
            this.router.navigate(['/']);
          }
        })
  }

  resetPassword() {
    this.passResetService.handleAsync(this.resetRequest).subscribe(
      {
        next: (data) => {
          this.successText = data;
          this.sentRequest = true;
        },
        error: (err) => {
          this.errorMessage = "Something went wrong";
        }
      });
  }

  validatePassword():boolean {
    let classList = document.getElementById("password")!.classList;
    if(!/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/.test(this.resetRequest.password)) {
      classList.add("invalid");
      classList.remove("valid");
      this.errorMessage = "Password must be at least 8 characters and contain one small and capital letter, number and special sign (@$!%*?&).";
      return false;
    }
    else {
      classList.add("valid");
      classList.remove("invalid");
      this.errorMessage = null;
      return true;
    }
  }

  validateIfSamePassword() : boolean {
    let classList = document.getElementById("repeatPassword")!.classList;
    if(this.repeatPassword !== this.resetRequest.password)
    {
      classList.add("invalid");
      classList.remove("valid");
      this.errorMessage = "Repeat passwords don't match";
      return false;
    }
    else
    {
      classList.add("valid");
      classList.remove("invalid");
      this.errorMessage = null;
      return true;
    }
  }

  checkButton():boolean {
    if(this.resetRequest.password !== "" && this.validatePassword() && this.validateIfSamePassword())
    {
      return false;
    }
    else {
      return true;
    }
  }
}
