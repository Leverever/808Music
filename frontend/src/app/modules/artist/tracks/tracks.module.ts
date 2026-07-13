import {CUSTOM_ELEMENTS_SCHEMA, NgModule} from '@angular/core';
import { CommonModule } from '@angular/common';

import { TracksRoutingModule } from './tracks-routing.module';
import {MatIcon} from '@angular/material/icon';
import {MatAnchor, MatButton, MatIconAnchor, MatIconButton} from '@angular/material/button';
import { TracksLayoutComponent } from './tracks-layout/tracks-layout.component';
import {
  MatCell,
  MatCellDef,
  MatColumnDef,
  MatHeaderCell,
  MatHeaderCellDef,
  MatHeaderRow, MatHeaderRowDef, MatRow, MatRowDef,
  MatTable
} from "@angular/material/table";
import {SharedModule} from "../../shared/shared.module";
import { TracksCreateOrEditComponent } from './tracks-create-or-edit/tracks-create-or-edit.component';
import {MatDatepicker, MatDatepickerInput, MatDatepickerToggle} from '@angular/material/datepicker';
import {MatFormField, MatSuffix} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {MatOption} from '@angular/material/core';
import {MatSelect} from '@angular/material/select';
import {MatSlideToggle} from '@angular/material/slide-toggle';
import {NgxAudioPlayerModule} from '@khajegan/ngx-audio-player';
import {MatAutocomplete, MatAutocompleteTrigger} from "@angular/material/autocomplete";
import {MatTooltip} from '@angular/material/tooltip';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import {MatPaginatorModule} from '@angular/material/paginator';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatDialogModule} from '@angular/material/dialog';
import {MatDividerModule} from '@angular/material/divider';
import {MatSnackBarModule} from '@angular/material/snack-bar';
import {MatTableModule} from '@angular/material/table';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import { TrackCatalogComponent } from './track-catalog/track-catalog.component';
import {
  FeaturedArtistSettingsDialogComponent,
  StemSetUploadDialogComponent,
  TrackDetailsComponent,
  TrackReleaseSettingsDialogComponent
} from './track-details/track-details.component';
import { TrackStatisticsComponent } from './track-statistics/track-statistics.component';
import {MatProgressBarModule} from '@angular/material/progress-bar';
import {MatSortModule} from '@angular/material/sort';


@NgModule({
  declarations: [
    TracksLayoutComponent,
    TracksCreateOrEditComponent,
    TrackCatalogComponent,
    TrackDetailsComponent,
    TrackStatisticsComponent,
    FeaturedArtistSettingsDialogComponent,
    TrackReleaseSettingsDialogComponent,
    StemSetUploadDialogComponent
  ],
  imports: [
    CommonModule,
    TracksRoutingModule,
    MatIcon,
    MatButton,
    MatIconButton,
    MatTable,
    MatColumnDef,
    MatHeaderCell,
    MatCell,
    MatHeaderCellDef,
    MatCellDef,
    MatHeaderRow,
    MatRow,
    MatRowDef,
    MatHeaderRowDef,
    SharedModule,
    MatAnchor,
    MatDatepicker,
    MatDatepickerInput,
    MatDatepickerToggle,
    MatFormField,
    MatInput,
    MatOption,
    MatSelect,
    MatSuffix,
    MatSlideToggle,
    NgxAudioPlayerModule,
    MatAutocomplete,
    MatAutocompleteTrigger,
    MatIconAnchor,
    MatTooltip,
    FormsModule,
    ReactiveFormsModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatDialogModule,
    MatDividerModule,
    MatSnackBarModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatProgressBarModule,
    MatSortModule,
  ],
  schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class TracksModule { }
