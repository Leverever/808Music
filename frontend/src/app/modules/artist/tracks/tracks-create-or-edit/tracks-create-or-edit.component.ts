import {Component, inject} from '@angular/core';
import {FormControl, FormGroup, Validators} from '@angular/forms';
import {Router} from '@angular/router';
import {MatSnackBar} from '@angular/material/snack-bar';
import {ArtistHandlerService} from '../../../../services/artist-handler.service';
import {TrackManagementV2EndpointService} from '../../../../endpoints/track-endpoints/track-management-v2-endpoint.service';

@Component({
  selector: 'app-tracks-create-or-edit',
  templateUrl: './tracks-create-or-edit.component.html',
  styleUrls: ['./tracks-create-or-edit.component.css']
})
export class TracksCreateOrEditComponent {
  readonly form = new FormGroup({
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(200)]
    }),
    isExplicit: new FormControl(false, {nonNullable: true}),
    masterFile: new FormControl<File | null>(null, Validators.required)
  });

  saving = false;
  errorMessage = '';
  private readonly snackBar = inject(MatSnackBar);

  constructor(
    private endpoint: TrackManagementV2EndpointService,
    private artistHandler: ArtistHandlerService,
    private router: Router
  ) {}

  setTrackFile(file: File | undefined): void {
    this.form.controls.masterFile.setValue(file ?? null);
    this.form.controls.masterFile.markAsTouched();
  }

  cancel(): void {
    this.router.navigate(['/artist/tracks']);
  }

  submit(): void {
    if (this.form.invalid || this.saving) {
      this.form.markAllAsTouched();
      return;
    }

    const artist = this.artistHandler.getSelectedArtist();
    const file = this.form.controls.masterFile.value;
    if (!artist || !file) return;

    this.saving = true;
    this.errorMessage = '';
    this.endpoint.upload(
      artist.id,
      this.form.controls.title.value.trim(),
      this.form.controls.isExplicit.value,
      file
    ).subscribe({
      next: () => {
        this.saving = false;
        this.snackBar.open('Track uploaded successfully.', 'Dismiss', {duration: 3000});
        this.router.navigate(['/artist/tracks']);
      },
      error: error => {
        this.saving = false;
        this.errorMessage = error?.error?.message ?? error?.error ?? 'The track could not be uploaded.';
      }
    });
  }
}
