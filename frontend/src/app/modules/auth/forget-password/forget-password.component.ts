import { Component } from '@angular/core';
import {PasswordResetRequest} from '../../../endpoints/auth-endpoints/user-password-reset-endpoint.service';
import {
  RequestForPasswordReset,
  UserRequestPasswordResetEndpointService
} from '../../../endpoints/auth-endpoints/user-request-password-reset-endpoint.service';


@Component({
  selector: 'app-forget-password',
  templateUrl: './forget-password.component.html',
  styleUrls: ['./forget-password.component.css', "../login/login.component.css"]

})
export class ForgetPasswordComponent {
  resetRequest: RequestForPasswordReset = {
    email: '',
  };
  errorMessage :string | null = null;
  class :string = "valid";
  sentRequest :boolean = false;

  constructor(private requestForReset: UserRequestPasswordResetEndpointService) {

  }
  sendResetRequest() {
    this.requestForReset.handleAsync(this.resetRequest).subscribe(
      {
        next: () => {
          this.sentRequest = true;
        },
        error: (err) => {
          this.errorMessage = err.message;
        }
      });
  }

  validateEmail():boolean {
    let classList = document.getElementById("email")!.classList;
    if(!/^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/.test(this.resetRequest.email)) {
      classList.add("invalid");
      classList.remove("valid");
      this.errorMessage = "Email is invalid!"
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

  checkButton() : boolean {
    return !(this.resetRequest.email !== "" && this.validateEmail());
  }
}
