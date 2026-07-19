import {ChangeDetectorRef, Component, OnInit} from '@angular/core';
import {UserService} from '../../../endpoints/user-endpoints/get-user-info-endpoints.service';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';
import {Router} from '@angular/router';
import {
  UserGetByIdEndpointService,
  UserGetResponse
} from '../../../endpoints/user-endpoints/user-get-by-id-endpoint.service';
import {FormControl, FormGroup, Validators} from '@angular/forms';
import {
  CountryGetAllEndpointService,
  CountryGetAllResponse
} from '../../../endpoints/country-endpoints/country-get-all-endpoint.service';
import {MAT_DATE_FORMATS, MAT_DATE_LOCALE, provideNativeDateAdapter} from '@angular/material/core';
import {MusicPlayerService} from '../../../services/music-player.service';
import {MatSlideToggleChange} from '@angular/material/slide-toggle';
import {
  UserPreferenceBottomSheetComponent
} from '../bottom-sheets/user-preference-bottom-sheet/user-preference-bottom-sheet.component';
import {MatBottomSheet} from '@angular/material/bottom-sheet';
import {MyConfig} from '../../../my-config';
import {PfpCropperDialogComponent} from '../pfp-cropper-dialog/pfp-cropper-dialog.component';
import {MatDialog} from '@angular/material/dialog';
import {TextInputDialogComponent} from '../dialogs/text-input-dialog/text-input-dialog.component';
import {
  UserRegisterOrUpdateEndpointService
} from '../../../endpoints/auth-endpoints/user-register-or-update-endpoint.service';
import {MatSnackBar} from '@angular/material/snack-bar';
import {
  ManageFollowingBottomSheetComponent
} from '../bottom-sheets/manage-following-bottom-sheet/manage-following-bottom-sheet.component';
import {TranslateService} from '@ngx-translate/core';

export class MyFormat {
  value = 2;
  constructor() {}
  get display() {
    return this.value == 1
      ? {
        dateInput: "YYYY/MM/DD",
        monthYearLabel: "MMM YYYY",
        dateA11yLabel: "LL",
        monthYearA11yLabel: "MMMM YYYY"
      }
      : {
        dateInput: "DD/MM/YYYY",
        monthYearLabel: "MM YYYY",
        dateA11yLabel: "DD/MM/YYYY",
        monthYearA11yLabel: "MM YYYY"
      };
  }
  get parse() {
    return this.value == 1
      ? {
        dateInput: "YYYY/MM/DD"
      }
      : {
        dateInput: "DD/MM/YYYY"
      };
  }
}

@Component({
  selector: 'app-settings',
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css',
  providers: [provideNativeDateAdapter(), {provide: MAT_DATE_LOCALE, useValue: 'en-GB'}]
})
export class SettingsComponent implements OnInit {
  userId: number = 0;
  user : UserGetResponse | null = null;
  countries : CountryGetAllResponse[] = [];
  autoplayStatus = false;

  form = new FormGroup({
    id: new FormControl(0),
    username: new FormControl('', [Validators.required, Validators.pattern(/^[a-zA-Z0-9.]{3,20}$/)]),
    email: new FormControl('', [Validators.required, Validators.pattern(/^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/)]),
    firstName: new FormControl('', [Validators.required, Validators.pattern(/^[a-zA-ZčČćĆđĐšŠžŽ'’\- ]{1,50}$/)]),
    lastName: new FormControl('', [Validators.required, Validators.pattern(/^[a-zA-ZčČćĆđĐšŠžŽ'’\- ]{1,50}$/)]),
    countryId: new FormControl(0, [Validators.required]),
    newPassword: new FormControl<string | undefined>(undefined, [Validators.minLength(8), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/)]),
    password: new FormControl('', [Validators.required, Validators.minLength(8), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$/)]),
    dateOfBirth: new FormControl('', [Validators.required]),
  })


  constructor(private auth: MyUserAuthService,
              private router: Router,
              private userGetService: UserGetByIdEndpointService,
              private countryService: CountryGetAllEndpointService,
              private musicPlayerService: MusicPlayerService,
              private bottomSheet: MatBottomSheet,
              private dialog: MatDialog,
              private cdRef: ChangeDetectorRef,
              private updateUserService: UserRegisterOrUpdateEndpointService,
              private snackBar: MatSnackBar,
              private translateService: TranslateService) {}

  ngOnInit(): void {
    this.userId = this.auth.getAuthToken()!.userId;

    this.autoplayStatus = this.musicPlayerService.getAutoPlayStatus();

    this.countryService.handleAsync().subscribe((data) => {
      this.countries = data;
    })

    this.userGetService.handleAsync(this.userId).subscribe({
      next: result => {
        this.user = result;
        this.form.patchValue({
          countryId: result.countryId,
          dateOfBirth: result.dateOfBirth,
          firstName: result.firstName,
          email: result.email,
          lastName: result.lastName,
          id: result.id,
          username: result.username
        })
      }
    })
  }

  openNotificationSettings() {
    this.bottomSheet.open(UserPreferenceBottomSheetComponent, {
      hasBackdrop: true,
      panelClass: ['liquid-glass-sheet-pane', 'listener-content-sheet-pane'],
      backdropClass: 'liquid-glass-sheet-backdrop',
    });
  }

  openImageCropperDialog(): void {
    const dialogRef = this.dialog.open(PfpCropperDialogComponent, {
      width: 'calc(100vw - 24px)',
      maxWidth: '560px',
      panelClass: 'liquid-settings-dialog-pane',
      backdropClass: 'liquid-settings-dialog-backdrop',
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        console.log('Profile picture uploaded successfully:', result);
        this.user!.pathToPfp = "/media" + result.profilePicturePath;
        this.cdRef.detectChanges();
      } else {
        console.error('Profile picture upload failed');
      }
    });
    this.cdRef.detectChanges();
  }

  changeAutoplayStatus(e: MatSlideToggleChange) {
    this.musicPlayerService.setAutoPlayStatus(e.checked);
  }

  updateUser() {

  }

  protected readonly MyConfig = MyConfig;

  openPasswordDialog() {
    let ref = this.dialog.open(TextInputDialogComponent, {
      width: 'calc(100vw - 24px)',
      maxWidth: '460px',
      panelClass: 'liquid-settings-dialog-pane',
      backdropClass: 'liquid-settings-dialog-backdrop',
      data: {
        title: this.getTranslation('SETTINGS.DIALOGS.PasswordTitle'),
        content: this.getTranslation('SETTINGS.DIALOGS.PasswordDescription'),
        type: "password",
        inputLabel: this.getTranslation('SETTINGS.DIALOGS.PasswordLabel'),
        placeholder: this.getTranslation('SETTINGS.DIALOGS.PasswordPlaceholder'),
      }
    })
    ref.afterClosed().subscribe({ next: result => {
      if(result) {
        this.form.get('password')!.setValue(result ?? undefined);
        if(!this.form.invalid)
        {
          this.updateUserService.handleAsync({
            iD : this.userId,
            password : this.form.value.password!,
            username : this.form.value.username!,
            email : this.form.value.email!,
            lastName : this.form.value.lastName!,
            firstName : this.form.value.firstName!,
            newPassword : (this.form.value.newPassword === null || this.form.value.newPassword === undefined || this.form.value.newPassword.trim() === "") ? undefined : this.form.value.newPassword,
            dateOfBirth : this.form.value.dateOfBirth!,
            countryId : this.form.value.countryId!,
          }).subscribe(data => {
            this.snackBar.open(data, "Dismiss", {duration: 2000})
          })
        }
        else {
          this.snackBar.open("Please fill out the required fields and input the correct password!", "Dismiss", {duration: 2000})
        }
      }
    }})
  }

  openFollowingSettings() {
    this.bottomSheet.open(ManageFollowingBottomSheetComponent, {
      hasBackdrop: true,
      panelClass: ['liquid-glass-sheet-pane', 'listener-content-sheet-pane'],
      backdropClass: 'liquid-glass-sheet-backdrop',
    });
  }

  getTranslation(key : string) : string {
    let value = "";
    this.translateService.get(key).subscribe(data => value = data);
    return value;
  }
}
